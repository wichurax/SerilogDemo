/**
 * k6 Load Test Script for SerilogDemo E-Commerce API
 * 
 * This script simulates high-throughput e-commerce traffic to generate
 * a large volume of structured logs for benchmarking Loki query performance.
 * 
 * Usage:
 *   docker compose --profile scaled --profile loadtest up -d --scale api-scaled=5
 *   
 *   Or run manually:
 *   k6 run --vus 100 --duration 10m load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Counter, Trend } from 'k6/metrics';
import { randomItem, randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

//-----------------------------------------------------------------------------
// Configuration
//-----------------------------------------------------------------------------

const BASE_URL = __ENV.API_URL || 'http://nginx:80';

// Test stages - ramp up, sustain, ramp down
export const options = {
    stages: [
        { duration: '1m', target: 50 },     // Ramp up to 50 users
        { duration: '2m', target: 200 },    // Ramp up to 200 users
        { duration: '3m', target: 300 },    // Sustain 300 users
        { duration: '5m', target: 200 },    // Scale down to 200 users
        { duration: '3m', target: 50 },     // Ramp down to 50 users
        { duration: '1m', target: 150 },    // Ramp up to 150 users
        { duration: '2m', target: 250 },    // Scale up to 250 users
        { duration: '3m30s', target: 350 },    // Ramp up to 350 users
        { duration: '30s', target: 500 },   // Peak at 500 users
        { duration: '2m', target: 400 },    // Scale down to 400 users
        { duration: '1m', target: 400 },    // Sustain 400 users
        { duration: '2m', target: 300 },    // Scale down to 300 users
        { duration: '2m', target: 250 },    // Scale down to 250 users
        { duration: '1m', target: 150 },    // Scale down to 150 users
        { duration: '3m', target: 50 },     // Ramp down to 50 users
        { duration: '3m', target: 10 },     // Ramp down to 10 users
        { duration: '3m', target: 100 },    // Ramp up to 100 users
        { duration: '2m', target: 200 },    // Ramp up to 200 users
        { duration: '5m', target: 250 },    // Ramp up to 250 users
        { duration: '3m', target: 100 },    // Ramp down to 100 users
        { duration: '2m', target: 0 },      // Ramp down to 0 users
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],  // 95% of requests under 500ms
        http_req_failed: ['rate<0.01'],    // Less than 1% failure rate
    },
};

//-----------------------------------------------------------------------------
// Custom Metrics
//-----------------------------------------------------------------------------

const orderPlacedCounter = new Counter('orders_placed');
const basketOperations = new Counter('basket_operations');
const itemsViewed = new Counter('items_viewed');
const orderPlacementDuration = new Trend('order_placement_duration');
const errorRate = new Rate('errors');

//-----------------------------------------------------------------------------
// Test Data
//-----------------------------------------------------------------------------

// Known item IDs from seed data (will be populated dynamically)
let itemIds = [];
let deliveryOptionIds = [];
let paymentOptionIds = [];

// User simulation
function generateUserId() {
    return `loadtest-user-${randomIntBetween(1, 10000)}-${Date.now()}`;
}

//-----------------------------------------------------------------------------
// API Helper Functions
//-----------------------------------------------------------------------------

const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
};

function getHeaders(userId) {
    return {
        ...headers,
        'X-User-Id': userId,
    };
}

function fetchItems() {
    const res = http.get(`${BASE_URL}/api/items`, { headers });
    if (res.status === 200) {
        try {
            const items = JSON.parse(res.body);
            return items.map(i => i.id);
        } catch (e) {
            return [];
        }
    }
    return [];
}

function fetchDeliveryOptions() {
    const res = http.get(`${BASE_URL}/api/deliveryoptions`, { headers });
    if (res.status === 200) {
        try {
            const options = JSON.parse(res.body);
            return options.map(o => o.id);
        } catch (e) {
            return [];
        }
    }
    return [];
}

function fetchPaymentOptions() {
    const res = http.get(`${BASE_URL}/api/paymentoptions`, { headers });
    if (res.status === 200) {
        try {
            const options = JSON.parse(res.body);
            return options.map(o => o.id);
        } catch (e) {
            return [];
        }
    }
    return [];
}

//-----------------------------------------------------------------------------
// Setup - Fetch reference data
//-----------------------------------------------------------------------------

export function setup() {
    console.log('Fetching reference data...');
    
    // Wait for API to be ready
    let retries = 10;
    while (retries > 0) {
        const healthRes = http.get(`${BASE_URL}/health`);
        if (healthRes.status === 200) {
            break;
        }
        console.log(`Waiting for API... (${retries} retries left)`);
        sleep(2);
        retries--;
    }
    
    const items = fetchItems();
    const delivery = fetchDeliveryOptions();
    const payment = fetchPaymentOptions();
    
    console.log(`Loaded ${items.length} items, ${delivery.length} delivery options, ${payment.length} payment options`);
    
    return {
        itemIds: items,
        deliveryOptionIds: delivery,
        paymentOptionIds: payment,
    };
}

//-----------------------------------------------------------------------------
// Scenarios
//-----------------------------------------------------------------------------

// Scenario 1: Browse items (most common)
function browseItems(data) {
    itemsViewed.add(1);
    
    // Get all items
    let res = http.get(`${BASE_URL}/api/items`, { headers });
    check(res, { 'items list OK': (r) => r.status === 200 });
    
    // Get categories
    res = http.get(`${BASE_URL}/api/items/categories`, { headers });
    check(res, { 'categories OK': (r) => r.status === 200 });
    
    // Get items by random category
    const categories = ['Electronics', 'Furniture', 'Accessories'];
    const category = randomItem(categories);
    res = http.get(`${BASE_URL}/api/items/categories/${category}`, { headers });
    check(res, { 'items by category OK': (r) => r.status === 200 });
    
    // View specific item
    if (data.itemIds.length > 0) {
        const itemId = randomItem(data.itemIds);
        res = http.get(`${BASE_URL}/api/items/${itemId}`, { headers });
        check(res, { 'item detail OK': (r) => r.status === 200 || r.status === 404 });
    }
    
    sleep(randomIntBetween(1, 3));
}

// Scenario 2: Add items to basket
function addToBasket(data, userId) {
    basketOperations.add(1);
    
    if (data.itemIds.length === 0) return;
    
    const itemId = randomItem(data.itemIds);
    const quantity = randomIntBetween(1, 5);
    
    const payload = JSON.stringify({
        itemId: itemId,
        quantity: quantity,
    });
    
    const res = http.post(`${BASE_URL}/api/basket/items`, payload, {
        headers: getHeaders(userId),
    });
    
    const success = check(res, { 
        'add to basket OK': (r) => r.status === 200 || r.status === 201 
    });
    
    if (!success) {
        errorRate.add(1);
    }
    
    sleep(randomIntBetween(0.5, 2));
}

// Scenario 3: View basket
function viewBasket(userId) {
    const res = http.get(`${BASE_URL}/api/basket`, {
        headers: getHeaders(userId),
    });
    
    check(res, { 'view basket OK': (r) => r.status === 200 });
    sleep(randomIntBetween(0.5, 1));
}

// Scenario 4: Complete checkout flow (most complex, most logs)
function completeCheckout(data, userId) {
    const startTime = Date.now();
    
    // Add 1-3 items to basket
    const numItems = randomIntBetween(1, 3);
    for (let i = 0; i < numItems; i++) {
        if (data.itemIds.length > 0) {
            const itemId = randomItem(data.itemIds);
            const payload = JSON.stringify({
                itemId: itemId,
                quantity: randomIntBetween(1, 3),
            });
            
            http.post(`${BASE_URL}/api/basket/items`, payload, {
                headers: getHeaders(userId),
            });
        }
    }
    
    // View basket
    http.get(`${BASE_URL}/api/basket`, { headers: getHeaders(userId) });
    
    // Check delivery options
    http.get(`${BASE_URL}/api/deliveryoptions`, { headers });
    
    // Check payment options
    http.get(`${BASE_URL}/api/paymentoptions`, { headers });
    
    // Place order
    if (data.deliveryOptionIds.length > 0 && data.paymentOptionIds.length > 0) {
        const orderPayload = JSON.stringify({
            deliveryOptionId: randomItem(data.deliveryOptionIds),
            paymentOptionId: randomItem(data.paymentOptionIds),
            shippingAddress: {
                street: `${randomIntBetween(1, 9999)} Test Street`,
                city: randomItem(['New York', 'Los Angeles', 'Chicago', 'Houston', 'Phoenix']),
                postalCode: `${randomIntBetween(10000, 99999)}`,
                country: 'USA',
            },
        });
        
        const res = http.post(`${BASE_URL}/api/orders`, orderPayload, {
            headers: getHeaders(userId),
        });
        
        const success = check(res, { 
            'order placed OK': (r) => r.status === 200 || r.status === 201 || r.status === 400 
        });
        
        if (res.status === 200 || res.status === 201) {
            orderPlacedCounter.add(1);
        }
        
        if (!success) {
            errorRate.add(1);
        }
    }
    
    // View orders
    http.get(`${BASE_URL}/api/orders`, { headers: getHeaders(userId) });
    
    const duration = Date.now() - startTime;
    orderPlacementDuration.add(duration);
    
    sleep(randomIntBetween(1, 3));
}

// Scenario 5: View orders (returning customer)
function viewOrders(userId) {
    const res = http.get(`${BASE_URL}/api/orders`, {
        headers: getHeaders(userId),
    });
    
    check(res, { 'view orders OK': (r) => r.status === 200 });
    sleep(randomIntBetween(0.5, 1));
}

//-----------------------------------------------------------------------------
// Main Test Function
//-----------------------------------------------------------------------------

export default function(data) {
    const userId = generateUserId();
    
    // Weighted scenario selection to simulate realistic traffic
    const scenario = Math.random();
    
    if (scenario < 0.40) {
        // 40% - Just browsing
        browseItems(data);
    } else if (scenario < 0.65) {
        // 25% - Browse and add to basket
        browseItems(data);
        addToBasket(data, userId);
    } else if (scenario < 0.80) {
        // 15% - View basket
        addToBasket(data, userId);
        viewBasket(userId);
    } else if (scenario < 0.95) {
        // 15% - Complete checkout
        completeCheckout(data, userId);
    } else {
        // 5% - Returning customer viewing orders
        viewOrders(userId);
    }
}

//-----------------------------------------------------------------------------
// Teardown
//-----------------------------------------------------------------------------

export function teardown(data) {
    console.log('Load test completed.');
    console.log(`Total orders attempted: ${orderPlacedCounter}`);
}
