export type PaymentScenario = 'Default' | 'Success' | 'Decline' | 'SlowSuccess' | 'Timeout'

export type FulfillmentStatus =
  | 'Pending'
  | 'Reserved'
  | 'Collected'
  | 'Packed'
  | 'Shipped'
  | 'Failed'

export interface Item {
  id: string
  name: string
  description: string
  price: number
  category: string
  imageUrl: string
  quantityOnHand: number
  quantityReserved: number
  availableQuantity: number
}

export interface BasketItem {
  id: string
  itemId: string
  itemName: string
  unitPrice: number
  quantity: number
  totalPrice: number
}

export interface Basket {
  id: string
  userId: string
  items: BasketItem[]
  totalPrice: number
  totalItems: number
  createdAt: string
  updatedAt: string
}

export interface DeliveryOption {
  id: string
  courierName: string
  name: string
  description: string
  price: number
  estimatedDaysMin: number
  estimatedDaysMax: number
}

export interface PaymentOption {
  id: string
  name: string
  description: string
  icon: string
}

export interface OrderItem {
  id: string
  itemId: string
  itemName: string
  unitPrice: number
  quantity: number
  totalPrice: number
}

export interface OrderFulfillment {
  status: string
  warehouseName?: string | null
  isPendingDelivery: boolean
  trackingReference?: string | null
  lastMessage?: string | null
  dispatchedAtUtc?: string | null
  lastUpdatedAtUtc?: string | null
}

export interface Order {
  id: string
  orderNumber: string
  userId: string
  items: OrderItem[]
  deliveryOption: DeliveryOption
  deliveryPrice: number
  paymentOption: PaymentOption
  itemsTotal: number
  totalPrice: number
  status: string
  paymentStatus: string
  paymentAttemptId?: string | null
  paymentProviderCode?: string | null
  paymentFailureReason?: string | null
  fulfillment: OrderFulfillment
  createdAt: string
}

export interface OrderSummary {
  orderId: string
  orderNumber: string
  totalPrice: number
  status: string
  fulfillmentStatus: string
  isPendingDelivery: boolean
  createdAt: string
}

export interface OrderPlacementFailure {
  message: string
  orderId: string
  orderNumber: string
  paymentStatus: string
  paymentProviderCode?: string | null
}

export type CheckoutResult =
  | {
      kind: 'authorized'
      order: Order
    }
  | {
      kind: 'declined' | 'timedOut'
      data: OrderPlacementFailure
    }

export interface WarehouseInventory {
  itemId: string
  itemName: string
  category: string
  warehouseName: string
  quantityOnHand: number
  quantityReserved: number
  availableQuantity: number
  updatedAtUtc: string
}

export interface WarehouseInventoryCommandResult {
  message: string
  quantityOnHand: number
  quantityReserved: number
  availableQuantity: number
}

export interface FulfillmentAttemptItem {
  itemId: string
  itemName: string
  quantity: number
}

export interface FulfillmentAttempt {
  orderId: string
  orderNumber: string
  userId: string
  warehouse: string
  deliveryCourier: string
  deliveryOptionName: string
  status: FulfillmentStatus
  trackingReference?: string | null
  createdAtUtc: string
  lastProcessedAtUtc: string
  collectedAtUtc?: string | null
  packedAtUtc?: string | null
  shippedAtUtc?: string | null
  items: FulfillmentAttemptItem[]
}