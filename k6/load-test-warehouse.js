/**
 * k6 Load Test Script for SerilogDemo Fulfillment API
 *
 * This script models warehouse workers that poll fulfillment backlog,
 * collect orders, pack them, and ship them through the Fulfillment API.
 * It is intended to run alongside the ecommerce customer load test.
 *
 * Usage:
 *   docker compose --profile loadtest up -d --scale api=5
 *
 *   Or run manually:
 *   k6 run --vus 20 --duration 10m load-test-warehouse.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

const FULFILLMENT_URL = __ENV.FULFILLMENT_URL || 'http://fulfillment-api:8080';
const BACKLOG_BATCH_SIZE = parsePositiveInt(__ENV.FULFILLMENT_BATCH_SIZE, 5);
const INSPECTOR_TAKE = parsePositiveInt(__ENV.FULFILLMENT_INSPECTOR_TAKE, 25);

export const options = {
    stages: [
        { duration: '1m', target: 10 },
        { duration: '4m', target: 25 },
        { duration: '3m', target: 40 },
        { duration: '2m', target: 15 },
        { duration: '1m', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<1500', 'p(99)<3000'],
        http_req_failed: ['rate<0.05'],
        fulfillment_transition_duration: ['p(95)<2500'],
        fulfillment_unexpected_errors: ['rate<0.02'],
    },
};

const backlogPolls = new Counter('fulfillment_backlog_polls');
const inspectorQueries = new Counter('fulfillment_inspector_queries');
const transitionAttempts = new Counter('fulfillment_transition_attempts');
const ordersCollected = new Counter('fulfillment_orders_collected');
const ordersPacked = new Counter('fulfillment_orders_packed');
const ordersShipped = new Counter('fulfillment_orders_shipped');
const transitionConflicts = new Counter('fulfillment_transition_conflicts');
const transitionDuration = new Trend('fulfillment_transition_duration');
const unexpectedErrors = new Rate('fulfillment_unexpected_errors');

const defaultHeaders = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
};

const okResponse = http.expectedStatuses(200);
const transitionResponse = http.expectedStatuses(200, 404, 409);
const healthProbeResponse = http.expectedStatuses({ min: 200, max: 599 });

let activeWorker = null;

export function setup() {
    waitForService();
    return null;
}

export default function () {
    const worker = getWorker();

    if (worker.role === 'collector') {
        processBacklog('Reserved', 'collect', worker, Math.min(BACKLOG_BATCH_SIZE, 5));
        return;
    }

    if (worker.role === 'packer') {
        processBacklog('Collected', 'pack', worker, Math.min(BACKLOG_BATCH_SIZE, 4));
        return;
    }

    if (worker.role === 'shipper') {
        processBacklog('Packed', 'ship', worker, Math.min(BACKLOG_BATCH_SIZE, 3));
        return;
    }

    runInspector(worker);
}

function processBacklog(status, action, worker, maxBatchSize) {
    const attempts = fetchAttempts(status, maxBatchSize);
    if (attempts.length === 0) {
        humanPause(3.0, 7.0);
        return;
    }

    const itemsToProcess = attempts.slice(0, randomIntBetween(1, attempts.length));
    for (let index = 0; index < itemsToProcess.length; index += 1) {
        applyTransition(action, itemsToProcess[index], worker);
        humanPause(0.4, worker.transitionPauseMaxSeconds);
    }

    humanPause(1.0, 3.5);
}

function runInspector(worker) {
    const queries = [
        `${FULFILLMENT_URL}/api/fulfillment/orders?take=${INSPECTOR_TAKE}`,
        `${FULFILLMENT_URL}/api/fulfillment/orders?status=Reserved&take=${INSPECTOR_TAKE}`,
        `${FULFILLMENT_URL}/api/fulfillment/orders?status=Collected&take=${INSPECTOR_TAKE}`,
        `${FULFILLMENT_URL}/api/fulfillment/orders?status=Packed&take=${INSPECTOR_TAKE}`,
        `${FULFILLMENT_URL}/api/fulfillment/orders?status=Shipped&take=${INSPECTOR_TAKE}`,
    ];

    const url = queries[randomIntBetween(0, queries.length - 1)];
    const res = http.get(url, { headers: defaultHeaders, responseCallback: okResponse });

    inspectorQueries.add(1);
    recordUnexpected(res.status === 200);
    check(res, { 'inspector query returned 200': (response) => response.status === 200 });

    if (res.status === 200 && Math.random() < 0.35) {
        const attempts = parseJson(res.body, []);
        if (attempts.length > 0) {
            const selected = attempts[randomIntBetween(0, attempts.length - 1)];
            const detailRes = http.get(`${FULFILLMENT_URL}/api/fulfillment/orders/${selected.orderId}`, {
                headers: defaultHeaders,
                responseCallback: okResponse,
            });

            inspectorQueries.add(1);
            recordUnexpected(detailRes.status === 200);
            check(detailRes, { 'inspector detail returned 200': (response) => response.status === 200 });
        }
    }

    humanPause(5.0, worker.transitionPauseMaxSeconds + 5);
}

function fetchAttempts(status, take) {
    const res = http.get(`${FULFILLMENT_URL}/api/fulfillment/orders?status=${encodeURIComponent(status)}&take=${take}`, {
        headers: defaultHeaders,
        responseCallback: okResponse,
    });

    backlogPolls.add(1);
    recordUnexpected(res.status === 200);
    check(res, { 'backlog poll returned 200': (response) => response.status === 200 });

    if (res.status !== 200) {
        return [];
    }

    const payload = parseJson(res.body, []);
    return Array.isArray(payload) ? payload : [];
}

function applyTransition(action, attempt, worker) {
    const startedAt = Date.now();
    const request = buildTransitionRequest(action, attempt, worker);
    const res = http.post(
        `${FULFILLMENT_URL}/api/fulfillment/orders/${attempt.orderId}/${action}`,
        JSON.stringify(request),
        {
            headers: defaultHeaders,
            responseCallback: transitionResponse,
        }
    );

    transitionAttempts.add(1, { action, role: worker.role });
    transitionDuration.add(Date.now() - startedAt, { action, role: worker.role });

    const accepted = res.status === 200 || res.status === 404 || res.status === 409;
    recordUnexpected(accepted);
    check(res, { 'transition returned 200, 404 or 409': (response) => accepted });

    if (res.status === 409 || res.status === 404) {
        transitionConflicts.add(1, { action, role: worker.role, status: String(res.status) });
        return;
    }

    if (action === 'collect') {
        ordersCollected.add(1);
        return;
    }

    if (action === 'pack') {
        ordersPacked.add(1);
        return;
    }

    ordersShipped.add(1);
}

function buildTransitionRequest(action, attempt, worker) {
    if (action === 'ship') {
        return {
            trackingReference: `K6-${worker.role.toUpperCase()}-${String(__VU).padStart(3, '0')}-${String(__ITER).padStart(6, '0')}`,
            message: `Shipped by ${worker.role} worker ${worker.workerId} during load test.`,
        };
    }

    const verb = action === 'collect' ? 'Collected' : 'Packed';
    return {
        message: `${verb} by ${worker.role} worker ${worker.workerId} during load test.`,
    };
}

function getWorker() {
    if (activeWorker !== null) {
        return activeWorker;
    }

    const workerIndex = __VU;
    const percentile = (workerIndex * 37) % 100;
    let role = 'collector';
    let transitionPauseMaxSeconds = 2.5;

    if (percentile >= 40 && percentile < 70) {
        role = 'packer';
        transitionPauseMaxSeconds = 4.5;
    } else if (percentile >= 70 && percentile < 90) {
        role = 'shipper';
        transitionPauseMaxSeconds = 3.5;
    } else if (percentile >= 90) {
        role = 'inspector';
        transitionPauseMaxSeconds = 10.0;
    }

    activeWorker = {
        workerId: `warehouse-worker-${String(workerIndex).padStart(3, '0')}`,
        role,
        transitionPauseMaxSeconds,
    };

    return activeWorker;
}

function waitForService() {
    for (let retries = 0; retries < 20; retries += 1) {
        const healthRes = http.get(`${FULFILLMENT_URL}/health`, {
            headers: defaultHeaders,
            responseCallback: healthProbeResponse,
        });

        if (healthRes.status === 200) {
            return;
        }

        console.log(`Waiting for Fulfillment API readiness... attempt ${retries + 1}/20`);
        sleep(2);
    }

    throw new Error(`Fulfillment API at ${FULFILLMENT_URL} did not become ready in time.`);
}

function parseJson(body, fallbackValue) {
    try {
        return JSON.parse(body);
    } catch (error) {
        return fallbackValue;
    }
}

function recordUnexpected(expected) {
    unexpectedErrors.add(expected ? 0 : 1);
}

function humanPause(minSeconds, maxSeconds) {
    const milliseconds = randomIntBetween(Math.round(minSeconds * 1000), Math.round(maxSeconds * 1000));
    sleep(milliseconds / 1000);
}

function parsePositiveInt(value, fallbackValue) {
    const parsed = Number.parseInt(value || '', 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallbackValue;
}