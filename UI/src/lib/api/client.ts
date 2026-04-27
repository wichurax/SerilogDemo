import { requestJson, requestWithStatus } from '@/lib/api/http'
import type {
  Cart,
  CheckoutResult,
  DeliveryOption,
  FulfillmentAttempt,
  Item,
  Order,
  OrderPlacementFailure,
  OrderSummary,
  PaymentOption,
  PaymentScenario,
  WarehouseInventory,
  WarehouseInventoryCommandResult,
} from '@/lib/api/types'

type ItemFilters = {
  category?: string
  search?: string
}

type FulfillmentFilters = {
  status?: string
  orderNumber?: string
  take?: number
}

function withQuery(path: string, searchParams: URLSearchParams) {
  const queryString = searchParams.toString()
  return queryString.length > 0 ? `${path}?${queryString}` : path
}

export function getCategories() {
  return requestJson<string[]>('/api/items/categories')
}

export function getItems(filters: ItemFilters = {}) {
  const searchParams = new URLSearchParams()

  if (filters.category) {
    searchParams.set('category', filters.category)
  }

  if (filters.search) {
    searchParams.set('search', filters.search)
  }

  return requestJson<Item[]>(withQuery('/api/items', searchParams))
}

export function getCart(userId: string) {
  return requestJson<Cart>('/api/cart', { userId })
}

export function addToCart(userId: string, input: { itemId: string; quantity: number }) {
  return requestJson<Cart>('/api/cart/items', {
    method: 'POST',
    userId,
    body: input,
  })
}

export function updateCartItem(userId: string, itemId: string, quantity: number) {
  return requestJson<Cart>(`/api/cart/items/${itemId}`, {
    method: 'PUT',
    userId,
    body: { quantity },
  })
}

export function removeCartItem(userId: string, itemId: string) {
  return requestJson<Cart>(`/api/cart/items/${itemId}`, {
    method: 'DELETE',
    userId,
  })
}

export function clearCart(userId: string) {
  return requestWithStatus<void>('/api/cart', [204], {
    method: 'DELETE',
    userId,
  })
}

export function getDeliveryOptions() {
  return requestJson<DeliveryOption[]>('/api/deliveryoptions')
}

export function getPaymentOptions() {
  return requestJson<PaymentOption[]>('/api/paymentoptions')
}

export async function placeOrder(
  userId: string,
  input: {
    deliveryOptionId: string
    paymentOptionId: string
    paymentScenario: PaymentScenario
  },
): Promise<CheckoutResult> {
  const headers = new Headers()

  if (input.paymentScenario !== 'Default') {
    headers.set('X-Payment-Scenario', input.paymentScenario)
  }

  const { status, data } = await requestWithStatus<Order | OrderPlacementFailure>(
    '/api/orders',
    [201, 202, 409],
    {
      method: 'POST',
      userId,
      headers,
      body: {
        deliveryOptionId: input.deliveryOptionId,
        paymentOptionId: input.paymentOptionId,
      },
    },
  )

  if (status === 201) {
    return {
      kind: 'authorized',
      order: data as Order,
    }
  }

  return {
    kind: status === 409 ? 'declined' : 'timedOut',
    data: data as OrderPlacementFailure,
  }
}

export function getOrders(userId: string) {
  return requestJson<OrderSummary[]>('/api/orders', { userId })
}

export function getOrder(userId: string, orderId: string) {
  return requestJson<Order>(`/api/orders/${orderId}`, { userId })
}

export function getOrderByNumber(userId: string, orderNumber: string) {
  return requestJson<Order>(`/api/orders/by-number/${encodeURIComponent(orderNumber)}`, { userId })
}

export function getFulfillmentAttempts(filters: FulfillmentFilters = {}) {
  const searchParams = new URLSearchParams()

  if (filters.status && filters.status !== 'All') {
    searchParams.set('status', filters.status)
  }

  if (filters.orderNumber) {
    searchParams.set('orderNumber', filters.orderNumber)
  }

  searchParams.set('take', String(filters.take ?? 50))

  return requestJson<FulfillmentAttempt[]>(withQuery('/api/fulfillment/orders', searchParams), {
    service: 'fulfillment',
  })
}

export function transitionFulfillment(
  orderId: string,
  action: 'collect' | 'pack' | 'ship' | 'fail',
  payload: { message?: string; trackingReference?: string },
) {
  return requestJson<FulfillmentAttempt>(`/api/fulfillment/orders/${orderId}/${action}`, {
    method: 'POST',
    service: 'fulfillment',
    body: payload,
  })
}

export function getWarehouseInventory() {
  return requestJson<WarehouseInventory[]>('/api/warehouse/items')
}

export function restockInventory(itemId: string, quantity: number, reason?: string) {
  return requestJson<WarehouseInventoryCommandResult>(`/api/warehouse/items/${itemId}/restock`, {
    method: 'POST',
    body: { quantity, reason },
  })
}

export function writeOffInventory(itemId: string, quantity: number, reason?: string) {
  return requestJson<WarehouseInventoryCommandResult>(`/api/warehouse/items/${itemId}/write-off`, {
    method: 'POST',
    body: { quantity, reason },
  })
}

export function recountInventory(itemId: string, quantityOnHand: number, reason?: string) {
  return requestJson<WarehouseInventoryCommandResult>(`/api/warehouse/items/${itemId}/recount`, {
    method: 'POST',
    body: { quantityOnHand, reason },
  })
}