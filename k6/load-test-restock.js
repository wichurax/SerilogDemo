/**
 * k6 Load Test Script for SerilogDemo Warehouse Replenishment
 *
 * This script models inbound warehouse restocking during a load run.
 * It keeps available stock above a configurable low-water mark by using
 * the existing warehouse restock API instead of bypassing inventory rules.
 *
 * Pair this with load-test.js and load-test-warehouse.js when you want
 * a long-running end-to-end load test that continues to create new orders.
 *
 * Usage:
 *   docker compose --profile loadtest up -d --scale api=5
 *
 *   Or run manually:
 *   k6 run --vus 1 --duration 10m load-test-restock.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

const API_URL = __ENV.API_URL || 'http://nginx:80';
const LOW_WATER_MARK = parsePositiveInt(__ENV.RESTOCK_LOW_WATER_MARK, 6);
const TARGET_AVAILABLE_QUANTITY = parsePositiveInt(__ENV.RESTOCK_TARGET_AVAILABLE_QUANTITY, 18);
const MAX_ITEMS_PER_CYCLE = parsePositiveInt(__ENV.RESTOCK_MAX_ITEMS_PER_CYCLE, 5);
const POLL_SECONDS = parsePositiveInt(__ENV.RESTOCK_POLL_SECONDS, 8);
const CATEGORY_FILTERS = parseCsv(__ENV.RESTOCK_CATEGORY_FILTER);

export const options = {
    stages: [
        { duration: '1m', target: 1 },
        { duration: '9m', target: 1 },
        { duration: '1m', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<1500', 'p(99)<3000'],
        http_req_failed: ['rate<0.05'],
        restock_unexpected_errors: ['rate<0.02'],
        restock_command_duration: ['p(95)<2000'],
    },
};

const warehousePolls = new Counter('restock_inventory_polls');
const restockCommands = new Counter('restock_commands');
const restockedUnits = new Counter('restock_units_added');
const restockSkipped = new Counter('restock_skipped_cycles');
const restockDuration = new Trend('restock_command_duration');
const unexpectedErrors = new Rate('restock_unexpected_errors');

const defaultHeaders = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
};

const okResponse = http.expectedStatuses(200);
const restockResponse = http.expectedStatuses(200, 404, 409);
const healthProbeResponse = http.expectedStatuses({ min: 200, max: 599 });

export function setup() {
    validateConfiguration();
    waitForApi();
    return null;
}

export default function () {
    const inventory = fetchWarehouseInventory();
    if (inventory.length === 0) {
        restockSkipped.add(1, { reason: 'no_inventory' });
        humanPause(POLL_SECONDS, POLL_SECONDS + 3);
        return;
    }

    const candidates = selectCandidates(inventory);
    if (candidates.length === 0) {
        restockSkipped.add(1, { reason: 'healthy_stock' });
        humanPause(POLL_SECONDS, POLL_SECONDS + 3);
        return;
    }

    const batchSize = Math.min(MAX_ITEMS_PER_CYCLE, candidates.length);
    for (let index = 0; index < batchSize; index += 1) {
        restockItem(candidates[index]);
        humanPause(0.4, 1.0);
    }

    humanPause(POLL_SECONDS, POLL_SECONDS + 3);
}

function fetchWarehouseInventory() {
    const res = http.get(`${API_URL}/api/warehouse/items`, {
        headers: defaultHeaders,
        responseCallback: okResponse,
    });

    warehousePolls.add(1);
    recordUnexpected(res.status === 200);
    check(res, { 'warehouse inventory returned 200': (response) => response.status === 200 });

    if (res.status !== 200) {
        return [];
    }

    const payload = parseJson(res.body, []);
    if (!Array.isArray(payload)) {
        return [];
    }

    if (CATEGORY_FILTERS.length === 0) {
        return payload;
    }

    return payload.filter((item) => CATEGORY_FILTERS.includes(String(item.category || '').toLowerCase()));
}

function selectCandidates(items) {
    return items
        .filter((item) => Number(item.availableQuantity) <= LOW_WATER_MARK)
        .sort((left, right) => {
            const availabilityDifference = Number(left.availableQuantity) - Number(right.availableQuantity);
            if (availabilityDifference !== 0) {
                return availabilityDifference;
            }

            const onHandDifference = Number(left.quantityOnHand) - Number(right.quantityOnHand);
            if (onHandDifference !== 0) {
                return onHandDifference;
            }

            return String(left.itemName || '').localeCompare(String(right.itemName || ''));
        });
}

function restockItem(item) {
    const quantityToAdd = TARGET_AVAILABLE_QUANTITY - Number(item.availableQuantity);
    if (quantityToAdd <= 0) {
        restockSkipped.add(1, { reason: 'already_at_target' });
        return;
    }

    const startedAt = Date.now();
    const res = http.post(
        `${API_URL}/api/warehouse/items/${item.itemId}/restock`,
        JSON.stringify({
            quantity: quantityToAdd,
            reason: `Load-test replenishment for ${item.itemName}.`,
        }),
        {
            headers: defaultHeaders,
            responseCallback: restockResponse,
        }
    );

    restockDuration.add(Date.now() - startedAt, { category: item.category || 'unknown' });

    const accepted = res.status === 200 || res.status === 404 || res.status === 409;
    recordUnexpected(accepted);
    check(res, { 'restock command returned 200, 404 or 409': (response) => accepted });

    if (res.status === 200) {
        restockCommands.add(1, { result: 'restocked', category: item.category || 'unknown' });
        restockedUnits.add(quantityToAdd, { category: item.category || 'unknown' });
        return;
    }

    const result = res.status === 409 ? 'conflict' : 'not_found';
    restockCommands.add(1, { result, category: item.category || 'unknown' });
}

function waitForApi() {
    for (let retries = 0; retries < 15; retries += 1) {
        const healthRes = http.get(`${API_URL}/health`, {
            headers: defaultHeaders,
            responseCallback: healthProbeResponse,
        });

        if (healthRes.status === 200) {
            return;
        }

        console.log(`Waiting for API readiness... attempt ${retries + 1}/15`);
        sleep(2);
    }

    throw new Error(`API at ${API_URL} did not become ready in time.`);
}

function validateConfiguration() {
    if (TARGET_AVAILABLE_QUANTITY <= LOW_WATER_MARK) {
        throw new Error('RESTOCK_TARGET_AVAILABLE_QUANTITY must be greater than RESTOCK_LOW_WATER_MARK.');
    }
}

function parseJson(body, fallbackValue) {
    try {
        return JSON.parse(body);
    } catch (error) {
        return fallbackValue;
    }
}

function parsePositiveInt(value, fallbackValue) {
    const parsed = Number.parseInt(value || '', 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallbackValue;
}

function parseCsv(value) {
    if (!value) {
        return [];
    }

    return value
        .split(',')
        .map((entry) => entry.trim().toLowerCase())
        .filter((entry) => entry.length > 0);
}

function recordUnexpected(expected) {
    unexpectedErrors.add(expected ? 0 : 1);
}

function humanPause(minSeconds, maxSeconds) {
    const milliseconds = randomIntBetween(Math.round(minSeconds * 1000), Math.round(maxSeconds * 1000));
    sleep(milliseconds / 1000);
}