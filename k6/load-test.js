/**
 * k6 Load Test Script for SerilogDemo E-Commerce API
 *
 * This script models short customer sessions instead of raw endpoint hammering.
 * Each iteration creates a fresh session identity so order volume is limited
 * only by test duration, while still preserving realistic browse, cart,
 * checkout, and post-purchase polling behavior.
 *
 * Pair this with load-test-warehouse.js when you want automated fulfillment
 * progression during a load run.
 *
 * Usage:
 *   docker compose --profile loadtest up -d --scale api=5
 *
 *   Or run manually:
 *   k6 run --vus 25 --duration 10m load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { randomItem, randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

const BASE_URL = __ENV.API_URL || 'http://nginx:80';
const BROWSER_TRAFFIC_SHARE = 0.05;
const SHOPPER_TRAFFIC_SHARE = 0.20;
const SHOPPER_ABANDON_RATE = 0.05;
const BUYER_PRECHECKOUT_POLL_RATE = 0.08;

export const options = {
    stages: [
        { duration: '1m', target: 25 },
        { duration: '4m', target: 60 },
        { duration: '3m', target: 100 },
        { duration: '2m', target: 40 },
        { duration: '1m', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<1500', 'p(99)<3000'],
        http_req_failed: ['rate<0.02'],
        checkout_duration: ['p(95)<4000'],
        unexpected_errors: ['rate<0.02'],
    },
};

const ordersPlaced = new Counter('orders_placed');
const checkoutAttempts = new Counter('checkout_attempts');
const checkoutDeclined = new Counter('checkout_declined');
const checkoutTimedOut = new Counter('checkout_timed_out');
const checkoutValidationFailed = new Counter('checkout_validation_failed');
const checkoutStockValidationFailed = new Counter('checkout_stock_validation_failed');
const cartOperations = new Counter('cart_operations');
const cartAbandonments = new Counter('cart_abandonments');
const orderStatusPolls = new Counter('order_status_polls');
const itemsViewed = new Counter('items_viewed');
const inventoryConflicts = new Counter('inventory_conflicts');
const catalogRefreshes = new Counter('catalog_refreshes');
const checkoutDuration = new Trend('checkout_duration');
const orderStatusPollDuration = new Trend('order_status_poll_duration');
const unexpectedErrors = new Rate('unexpected_errors');

const defaultHeaders = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
};

const okResponse = http.expectedStatuses(200);
const itemDetailResponse = http.expectedStatuses(200, 404);
const cartMutationResponse = http.expectedStatuses(200, 201, 409);
const cartClearResponse = http.expectedStatuses(204);
const checkoutResponse = http.expectedStatuses(201, 202, 400, 409);
const healthProbeResponse = http.expectedStatuses({ min: 200, max: 599 });

let activeCatalog = null;

export function setup() {
    console.log('Fetching live reference data for customer journeys...');
    waitForApi();

    const categories = fetchJsonArray('/api/items/categories', 'categories');
    const items = fetchJsonArray('/api/items', 'items');
    const deliveryOptions = fetchJsonArray('/api/deliveryoptions', 'delivery options');
    const paymentOptions = fetchJsonArray('/api/paymentoptions', 'payment options');

    if (categories.length === 0 || items.length === 0 || deliveryOptions.length === 0 || paymentOptions.length === 0) {
        throw new Error('Reference data is incomplete. Ensure the API is seeded and reachable before running k6.');
    }

    console.log(
        `Loaded ${categories.length} categories, ${items.length} items, ${deliveryOptions.length} delivery options, ${paymentOptions.length} payment options.`
    );

    const searchTerms = buildSearchTerms(categories, items);

    return {
        categories,
        items,
        deliveryOptions,
        paymentOptions,
        searchTerms,
    };
}

export default function (data) {
    const persona = getPersona(data);

    if (persona.role === 'browser') {
        runBrowserJourney(data, persona);
        return;
    }

    if (persona.role === 'shopper') {
        runShopperJourney(data, persona);
        return;
    }

    runBuyerJourney(data, persona);
}

export function teardown() {
    console.log('Human-like load test completed.');
}

function runBrowserJourney(data, persona) {
    browseCatalog(data, persona, randomIntBetween(2, 4));

    if (Math.random() < 0.18) {
        const CartReady = buildCartForSession(data, persona, randomIntBetween(1, 2));
        if (CartReady) {
            viewCart(persona.userId);

            if (Math.random() < 0.6) {
                checkoutCart(data, persona);
            } else {
                cartAbandonments.add(1);
            }
        }
    } else if (persona.lastOrderId && Math.random() < 0.1) {
        checkRecentOrders(persona);
    }

    humanPause(1.5, 4.0);
}

function runShopperJourney(data, persona) {
    browseCatalog(data, persona, randomIntBetween(1, 3));

    const CartReady = buildCartForSession(data, persona, randomIntBetween(1, 3));
    if (!CartReady) {
        humanPause(1.0, 2.5);
        return;
    }

    viewCart(persona.userId);

    if (Math.random() < SHOPPER_ABANDON_RATE) {
        cartAbandonments.add(1);

        if (Math.random() < 0.35) {
            clearCart(persona.userId);
        }

        humanPause(2.0, 4.5);
        return;
    }

    checkoutCart(data, persona);
    humanPause(2.0, 5.0);
}

function runBuyerJourney(data, persona) {
    if (persona.lastOrderId && Math.random() < BUYER_PRECHECKOUT_POLL_RATE) {
        pollOrderStatus(persona, randomIntBetween(1, 2));
    }

    browseCatalog(data, persona, randomIntBetween(1, 2));

    const CartReady = buildCartForSession(data, persona, randomIntBetween(2, 4));
    if (!CartReady) {
        humanPause(1.0, 2.5);
        return;
    }

    checkoutCart(data, persona);
    humanPause(2.0, 4.5);
}

function browseCatalog(data, persona, steps) {
    const catalogRes = http.get(`${BASE_URL}/api/items`, { headers: defaultHeaders, responseCallback: okResponse });
    recordUnexpected(catalogRes.status === 200);
    check(catalogRes, { 'catalog list returned 200': (res) => res.status === 200 });

    if (catalogRes.status === 200) {
        syncCatalogFromBody(catalogRes.body);
    }

    if (Math.random() < 0.75) {
        const categoriesRes = http.get(`${BASE_URL}/api/items/categories`, { headers: defaultHeaders, responseCallback: okResponse });
        recordUnexpected(categoriesRes.status === 200);
        check(categoriesRes, { 'category list returned 200': (res) => res.status === 200 });
    }

    for (let index = 0; index < steps; index += 1) {
        if (data.searchTerms.length > 0 && Math.random() < 0.35) {
            const searchTerm = pickSearchTerm(data, persona);
            const searchRes = http.get(`${BASE_URL}/api/items?search=${encodeURIComponent(searchTerm)}`, {
                headers: defaultHeaders,
                responseCallback: okResponse,
            });

            recordUnexpected(searchRes.status === 200);
            check(searchRes, { 'catalog search returned 200': (res) => res.status === 200 });

            if (searchRes.status === 200) {
                syncCatalogFromBody(searchRes.body);
            }

            humanPause(0.6, 1.6);
        }

        const category = pickCategory(data, persona);
        const categoryPath = encodeURIComponent(category);
        let categoryRes;

        if (Math.random() < 0.5) {
            categoryRes = http.get(`${BASE_URL}/api/items?category=${categoryPath}`, { headers: defaultHeaders, responseCallback: okResponse });
        } else {
            categoryRes = http.get(`${BASE_URL}/api/items/categories/${categoryPath}`, {
                headers: defaultHeaders,
                responseCallback: okResponse,
            });
        }

        recordUnexpected(categoryRes.status === 200);
        check(categoryRes, { 'category browse returned 200': (res) => res.status === 200 });

        const item = pickItem(data, persona, category);
        if (item) {
            const detailRes = http.get(`${BASE_URL}/api/items/${item.id}`, {
                headers: defaultHeaders,
                responseCallback: itemDetailResponse,
            });

            itemsViewed.add(1);
            recordUnexpected(detailRes.status === 200 || detailRes.status === 404);
            check(detailRes, { 'item detail returned 200 or 404': (res) => res.status === 200 || res.status === 404 });
        }

        humanPause(0.8, 2.3);
    }
}

function buildCartForSession(data, persona, targetItems) {
    let cart = getCart(persona.userId);

    if (cart.totalItems > 5 || Math.random() < 0.12) {
        clearCart(persona.userId);
        cart = emptyCart(persona.userId);
    }

    let selectedItems = pickDistinctItems(data, persona, targetItems);
    if (selectedItems.length === 0) {
        refreshCatalogSnapshot();
        selectedItems = pickDistinctItems(data, persona, targetItems);
    }

    let successfulAdds = 0;

    for (let index = 0; index < selectedItems.length; index += 1) {
        const item = selectedItems[index];
        const quantity = chooseCartQuantity(item);
        if (addToCart(persona.userId, item.id, quantity)) {
            successfulAdds += 1;
        }

        if (Math.random() < 0.35) {
            humanPause(0.5, 1.5);
        }
    }

    cart = getCart(persona.userId);
    if (cart.items.length > 0 && Math.random() < 0.22) {
        tweakCart(persona.userId, cart);
        cart = getCart(persona.userId);
    }

    return successfulAdds > 0 && cart.totalItems > 0;
}

function tweakCart(userId, cart) {
    const cartItem = randomItem(cart.items);
    if (!cartItem) {
        return;
    }

    if (Math.random() < 0.35) {
        const deleteRes = http.del(`${BASE_URL}/api/cart/items/${cartItem.itemId}`, null, {
            headers: getHeaders(userId),
            responseCallback: okResponse,
        });

        cartOperations.add(1);
        recordUnexpected(deleteRes.status === 200);
        check(deleteRes, { 'cart remove returned 200': (res) => res.status === 200 });
        return;
    }

    const nextQuantity = Math.max(1, randomIntBetween(1, Math.max(2, cartItem.quantity + 1)));
    const updateRes = http.put(
        `${BASE_URL}/api/cart/items/${cartItem.itemId}`,
        JSON.stringify({ quantity: nextQuantity }),
        {
            headers: getHeaders(userId),
            responseCallback: cartMutationResponse,
        }
    );

    cartOperations.add(1);
    if (updateRes.status === 409) {
        inventoryConflicts.add(1);
    }

    recordUnexpected(updateRes.status === 200 || updateRes.status === 409);
    check(updateRes, { 'cart update returned 200 or 409': (res) => res.status === 200 || res.status === 409 });
}

function addToCart(userId, itemId, quantity) {
    const res = http.post(
        `${BASE_URL}/api/cart/items`,
        JSON.stringify({ itemId, quantity }),
        {
            headers: getHeaders(userId),
            responseCallback: cartMutationResponse,
        }
    );

    cartOperations.add(1);

    if (res.status === 409) {
        inventoryConflicts.add(1);
    }

    const accepted = res.status === 200 || res.status === 201 || res.status === 409;
    recordUnexpected(accepted);
    check(res, { 'cart add returned 200, 201 or 409': (response) => accepted });

    return res.status === 200 || res.status === 201;
}

function viewCart(userId) {
    const res = http.get(`${BASE_URL}/api/cart`, {
        headers: getHeaders(userId),
        responseCallback: okResponse,
    });

    recordUnexpected(res.status === 200);
    check(res, { 'cart view returned 200': (response) => response.status === 200 });
    return parseJson(res.body, emptyCart(userId));
}

function clearCart(userId) {
    const res = http.del(`${BASE_URL}/api/cart`, null, {
        headers: getHeaders(userId),
        responseCallback: cartClearResponse,
    });

    cartOperations.add(1);
    recordUnexpected(res.status === 204);
    check(res, { 'cart clear returned 204': (response) => response.status === 204 });
}

function checkoutCart(data, persona) {
    checkoutAttempts.add(1);

    const deliveryRes = http.get(`${BASE_URL}/api/deliveryoptions`, { headers: defaultHeaders, responseCallback: okResponse });
    const paymentRes = http.get(`${BASE_URL}/api/paymentoptions`, { headers: defaultHeaders, responseCallback: okResponse });

    recordUnexpected(deliveryRes.status === 200);
    recordUnexpected(paymentRes.status === 200);
    check(deliveryRes, { 'delivery options returned 200': (res) => res.status === 200 });
    check(paymentRes, { 'payment options returned 200': (res) => res.status === 200 });

    const deliveryOption = chooseDeliveryOption(data.deliveryOptions);
    const paymentOption = choosePaymentOption(data.paymentOptions);
    const paymentScenario = choosePaymentScenario();
    const startedAt = Date.now();

    const res = http.post(
        `${BASE_URL}/api/orders`,
        JSON.stringify({
            deliveryOptionId: deliveryOption.id,
            paymentOptionId: paymentOption.id,
        }),
        {
            headers: getHeaders(persona.userId, { 'X-Payment-Scenario': paymentScenario }),
            responseCallback: checkoutResponse,
        }
    );

    checkoutDuration.add(Date.now() - startedAt, { payment_scenario: paymentScenario.toLowerCase() });

    const payload = parseJson(res.body, {});

    if (res.status === 201) {
        ordersPlaced.add(1);
        persona.lastOrderId = payload.id;
        persona.lastOrderNumber = payload.orderNumber;
        persona.lastPaymentOutcome = 'authorized';
        recordUnexpected(true);
        check(res, { 'checkout authorized with 201': (response) => response.status === 201 });
        pollOrderStatus(persona, randomIntBetween(2, 4));
        return;
    }

    if (res.status === 400) {
        checkoutValidationFailed.add(1);
        persona.lastPaymentOutcome = 'validation_failed';

        const isStockFailure = isStockValidationFailure(payload.message);
        if (isStockFailure) {
            checkoutStockValidationFailed.add(1);
            refreshCatalogSnapshot();

            if (Math.random() < 0.5) {
                clearCart(persona.userId);
            }
        }

        recordUnexpected(true);
        check(res, { 'checkout validation returned 400': (response) => response.status === 400 });
        return;
    }

    if (res.status === 409) {
        checkoutDeclined.add(1);
        persona.lastPaymentOutcome = 'declined';
        recordUnexpected(true);
        check(res, { 'checkout decline returned 409': (response) => response.status === 409 });

        if (Math.random() < 0.35) {
            clearCart(persona.userId);
        }

        return;
    }

    if (res.status === 202) {
        checkoutTimedOut.add(1);
        persona.lastPaymentOutcome = 'timed_out';
        recordUnexpected(true);
        check(res, { 'checkout timeout returned 202': (response) => response.status === 202 });

        if (Math.random() < 0.25) {
            clearCart(persona.userId);
        }

        return;
    }

    recordUnexpected(false);
    check(res, { 'checkout returned an expected status': (response) => response.status === 201 || response.status === 202 || response.status === 409 });
}

function checkRecentOrders(persona) {
    const listRes = http.get(`${BASE_URL}/api/orders`, {
        headers: getHeaders(persona.userId),
        responseCallback: okResponse,
    });

    recordUnexpected(listRes.status === 200);
    check(listRes, { 'orders list returned 200': (res) => res.status === 200 });

    if (persona.lastOrderId && Math.random() < 0.65) {
        pollOrderStatus(persona, 1);
    }
}

function pollOrderStatus(persona, polls) {
    if (!persona.lastOrderId) {
        return;
    }

    for (let attempt = 0; attempt < polls; attempt += 1) {
        humanPause(2.0, 6.0);

        const startedAt = Date.now();
        const res = http.get(`${BASE_URL}/api/orders/${persona.lastOrderId}`, {
            headers: getHeaders(persona.userId),
            responseCallback: okResponse,
        });

        orderStatusPolls.add(1);
        orderStatusPollDuration.add(Date.now() - startedAt);

        const expected = res.status === 200;
        recordUnexpected(expected);
        check(res, { 'order poll returned 200': (response) => response.status === 200 });

        if (!expected) {
            return;
        }

        const order = parseJson(res.body, null);
        if (!order || !order.fulfillment || !order.fulfillment.status) {
            return;
        }

        if (order.fulfillment.status === 'Shipped') {
            return;
        }
    }
}

function getCart(userId) {
    const res = http.get(`${BASE_URL}/api/cart`, {
        headers: getHeaders(userId),
        responseCallback: okResponse,
    });

    recordUnexpected(res.status === 200);
    if (res.status !== 200) {
        return emptyCart(userId);
    }

    return parseJson(res.body, emptyCart(userId));
}

function waitForApi() {
    for (let retries = 0; retries < 15; retries += 1) {
        const healthRes = http.get(`${BASE_URL}/health`, { headers: defaultHeaders, responseCallback: healthProbeResponse });
        if (healthRes.status === 200) {
            return;
        }

        console.log(`Waiting for API readiness... attempt ${retries + 1}/15`);
        sleep(2);
    }

    throw new Error(`API at ${BASE_URL} did not become ready in time.`);
}

function fetchJsonArray(path, label) {
    const res = http.get(`${BASE_URL}${path}`, { headers: defaultHeaders, responseCallback: okResponse });

    if (res.status !== 200) {
        throw new Error(`Failed to load ${label}. GET ${path} returned ${res.status}.`);
    }

    const payload = parseJson(res.body, []);
    if (!Array.isArray(payload)) {
        throw new Error(`Expected ${label} to be a JSON array.`);
    }

    return payload;
}

function getPersona(data) {
    const sessionNumber = __ITER + 1;
    const sessionOrdinal = ((__VU - 1) * 1000000) + sessionNumber;
    const seed = (sessionOrdinal * 37) % 100;
    let role = 'browser';

    if (seed >= BROWSER_TRAFFIC_SHARE * 100 && seed < (BROWSER_TRAFFIC_SHARE + SHOPPER_TRAFFIC_SHARE) * 100) {
        role = 'shopper';
    } else if (seed >= (BROWSER_TRAFFIC_SHARE + SHOPPER_TRAFFIC_SHARE) * 100) {
        role = 'buyer';
    }

    return {
        userId: `loadtest-vu-${String(__VU).padStart(3, '0')}-session-${String(sessionNumber).padStart(6, '0')}`,
        role,
        favoriteCategory: data.categories[(sessionOrdinal - 1) % data.categories.length],
        favoriteSearchTerm: data.searchTerms[(sessionOrdinal - 1) % data.searchTerms.length],
        lastOrderId: null,
        lastOrderNumber: null,
        lastPaymentOutcome: null,
    };
}

function pickCategory(data, persona) {
    if (persona.favoriteCategory && Math.random() < 0.65) {
        return persona.favoriteCategory;
    }

    return randomItem(data.categories);
}

function pickSearchTerm(data, persona) {
    if (persona.favoriteSearchTerm && Math.random() < 0.6) {
        return persona.favoriteSearchTerm;
    }

    return randomItem(data.searchTerms);
}

function pickItem(data, persona, category) {
    const availableItems = getCatalogItems(data).filter((item) => item.availableQuantity === undefined || item.availableQuantity > 0);
    const categoryItems = availableItems.filter((item) => item.category === category);
    const favoriteItems = availableItems.filter((item) => item.category === persona.favoriteCategory);

    if (categoryItems.length > 0 && Math.random() < 0.7) {
        return randomItem(categoryItems);
    }

    if (favoriteItems.length > 0 && Math.random() < 0.6) {
        return randomItem(favoriteItems);
    }

    return availableItems.length > 0 ? randomItem(availableItems) : randomItem(getCatalogItems(data));
}

function buildSearchTerms(categories, items) {
    const itemTokens = items.flatMap((item) =>
        String(item.name || '')
            .split(/[^A-Za-z0-9]+/)
            .map((word) => word.trim())
            .filter((word) => word.length >= 4)
            .slice(0, 2)
    );

    return Array.from(new Set([...categories, ...itemTokens])).sort();
}

function pickDistinctItems(data, persona, count) {
    const catalogItems = getCatalogItems(data);
    const selected = [];
    const usedIds = {};

    while (selected.length < count) {
        const item = pickItem(data, persona, pickCategory(data, persona));
        if (!item) {
            break;
        }

        if (!usedIds[item.id]) {
            usedIds[item.id] = true;
            selected.push(item);
            continue;
        }

        if (Object.keys(usedIds).length >= catalogItems.length) {
            break;
        }
    }

    return selected;
}

function chooseDeliveryOption(optionsList) {
    const express = optionsList.filter((option) => /express/i.test(option.name));
    const locker = optionsList.filter((option) => /locker/i.test(option.name));
    const standard = optionsList.filter((option) => !/express|locker/i.test(option.name));
    const roll = Math.random();

    if (roll < 0.55 && standard.length > 0) {
        return randomItem(standard);
    }

    if (roll < 0.80 && express.length > 0) {
        return randomItem(express);
    }

    if (locker.length > 0) {
        return randomItem(locker);
    }

    return randomItem(optionsList);
}

function choosePaymentOption(optionsList) {
    const creditCard = optionsList.filter((option) => option.icon === 'credit-card');
    const paypal = optionsList.filter((option) => option.icon === 'paypal');
    const bankTransfer = optionsList.filter((option) => option.icon === 'bank');
    const roll = Math.random();

    if (roll < 0.65 && creditCard.length > 0) {
        return randomItem(creditCard);
    }

    if (roll < 0.90 && paypal.length > 0) {
        return randomItem(paypal);
    }

    if (bankTransfer.length > 0) {
        return randomItem(bankTransfer);
    }

    return randomItem(optionsList);
}

function choosePaymentScenario() {
    const roll = Math.random();

    if (roll < 0.88) {
        return 'Success';
    }

    if (roll < 0.94) {
        return 'Decline';
    }

    if (roll < 0.97) {
        return 'SlowSuccess';
    }

    return 'Timeout';
}

function chooseCartQuantity(item) {
    const availableQuantity = Number.isFinite(item.availableQuantity) ? item.availableQuantity : 3;
    const upperBound = Math.max(1, Math.min(3, availableQuantity));
    return randomIntBetween(1, upperBound);
}

function getCatalogItems(data) {
    return activeCatalog && activeCatalog.length > 0 ? activeCatalog : data.items;
}

function refreshCatalogSnapshot() {
    const res = http.get(`${BASE_URL}/api/items`, {
        headers: defaultHeaders,
        responseCallback: okResponse,
    });

    recordUnexpected(res.status === 200);
    check(res, { 'catalog refresh returned 200': (response) => response.status === 200 });

    if (res.status === 200) {
        syncCatalogFromBody(res.body);
    }
}

function syncCatalogFromBody(body) {
    const payload = parseJson(body, null);
    if (!Array.isArray(payload) || payload.length === 0) {
        return;
    }

    activeCatalog = payload;
    catalogRefreshes.add(1);
}

function isStockValidationFailure(message) {
    if (typeof message !== 'string') {
        return false;
    }

    return /out of stock|available in the warehouse|not stocked in warehouse/i.test(message);
}

function getHeaders(userId, extraHeaders) {
    return {
        ...defaultHeaders,
        'X-User-Id': userId,
        ...(extraHeaders || {}),
    };
}

function emptyCart(userId) {
    return {
        id: null,
        userId,
        items: [],
        totalPrice: 0,
        totalItems: 0,
    };
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



