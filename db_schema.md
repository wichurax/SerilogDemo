# Database Schema Overview

This repository uses one PostgreSQL database with four logical schemas owned by different services.

- `public`: main e-commerce API data
- `payment_gateway`: synchronous payment service data
- `fulfillment_service`: async fulfillment service data
- `notification_service`: async notification service data

Migration history tables are omitted from the diagram for clarity.

```mermaid
erDiagram
  PUBLIC_ITEMS {
    uuid Id PK
    string Name
    decimal Price
    string Category
  }

  PUBLIC_WAREHOUSE_INVENTORIES {
    uuid Id PK
    uuid ItemId FK
    string WarehouseName
    int QuantityOnHand
    int QuantityReserved
  }

  PUBLIC_BASKETS {
    uuid Id PK
    string UserId
    datetime CreatedAt
    datetime UpdatedAt
  }

  PUBLIC_BASKET_ITEMS {
    uuid Id PK
    uuid BasketId FK
    uuid ItemId FK
    int Quantity
    decimal UnitPrice
  }

  PUBLIC_DELIVERY_OPTIONS {
    uuid Id PK
    string CourierName
    string Name
    decimal Price
  }

  PUBLIC_PAYMENT_OPTIONS {
    uuid Id PK
    string Name
    string Icon
    bool IsActive
  }

  PUBLIC_ORDERS {
    uuid Id PK
    string OrderNumber UK
    string UserId
    uuid DeliveryOptionId FK
    uuid PaymentOptionId FK
    uuid PaymentAttemptId
    decimal TotalPrice
    string PaymentProviderCode
  }

  PUBLIC_ORDER_ITEMS {
    uuid Id PK
    uuid OrderId FK
    uuid ItemId
    int Quantity
    decimal UnitPrice
  }

  PUBLIC_INBOX_MESSAGES {
    uuid Id PK
    string MessageId UK
    string Type
    datetime ProcessedAtUtc
  }

  PUBLIC_OUTBOX_MESSAGES {
    uuid Id PK
    string Type
    string RoutingKey
    datetime OccurredAtUtc
    datetime PublishedAtUtc
  }

  PAYMENT_GATEWAY_PAYMENT_ATTEMPTS {
    uuid Id PK
    string IdempotencyKey UK
    string OrderReference
    string UserId
    string PaymentMethodCode
    decimal Amount
    string ProviderCode
    string Status
  }

  FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPTS {
    uuid Id PK
    string MessageId UK
    uuid OrderId
    string OrderNumber
    string UserId
    string Warehouse
    string Status
    string TrackingReference
  }

  FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPT_ITEMS {
    uuid Id PK
    uuid FulfillmentAttemptId FK
    uuid ItemId
    string ItemName
    int Quantity
  }

  FULFILLMENT_SERVICE_OUTBOX_MESSAGES {
    uuid Id PK
    string Type
    string RoutingKey
    datetime OccurredAtUtc
    datetime PublishedAtUtc
  }

  NOTIFICATION_SERVICE_NOTIFICATION_USERS {
    string UserId PK
    string Email
    string PhoneNumber
    bool EmailEnabled
    bool SmsEnabled
  }

  NOTIFICATION_SERVICE_EMAIL_NOTIFICATION_DELIVERIES {
    uuid Id PK
    string MessageId UK
    uuid OrderId
    string OrderNumber
    string UserId
    string RecipientEmail
    string Status
  }

  NOTIFICATION_SERVICE_SMS_NOTIFICATION_DELIVERIES {
    uuid Id PK
    string MessageId UK
    uuid OrderId
    string OrderNumber
    string UserId
    string RecipientPhoneNumber
    string Status
  }

  PUBLIC_ITEMS ||--o{ PUBLIC_WAREHOUSE_INVENTORIES : stocked_as
  PUBLIC_BASKETS ||--o{ PUBLIC_BASKET_ITEMS : contains
  PUBLIC_ITEMS ||--o{ PUBLIC_BASKET_ITEMS : referenced_by
  PUBLIC_DELIVERY_OPTIONS ||--o{ PUBLIC_ORDERS : selected_for
  PUBLIC_PAYMENT_OPTIONS ||--o{ PUBLIC_ORDERS : selected_for
  PUBLIC_ORDERS ||--o{ PUBLIC_ORDER_ITEMS : contains
  FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPTS ||--o{ FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPT_ITEMS : contains

  PUBLIC_ORDERS ||..o{ PAYMENT_GATEWAY_PAYMENT_ATTEMPTS : payment_reference
  PUBLIC_ORDERS ||..o{ FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPTS : fulfillment_projection
  PUBLIC_ORDERS ||..o{ NOTIFICATION_SERVICE_EMAIL_NOTIFICATION_DELIVERIES : email_projection
  PUBLIC_ORDERS ||..o{ NOTIFICATION_SERVICE_SMS_NOTIFICATION_DELIVERIES : sms_projection
  PUBLIC_ORDERS ||..|| NOTIFICATION_SERVICE_NOTIFICATION_USERS : user_preference_lookup
  PUBLIC_OUTBOX_MESSAGES ||..o{ FULFILLMENT_SERVICE_FULFILLMENT_ATTEMPTS : order_paid_event
  PUBLIC_OUTBOX_MESSAGES ||..o{ NOTIFICATION_SERVICE_EMAIL_NOTIFICATION_DELIVERIES : order_paid_event
  PUBLIC_OUTBOX_MESSAGES ||..o{ NOTIFICATION_SERVICE_SMS_NOTIFICATION_DELIVERIES : order_paid_event
  FULFILLMENT_SERVICE_OUTBOX_MESSAGES ||..o{ PUBLIC_INBOX_MESSAGES : fulfillment_progress_event
```

Schema prefix legend:

- `PUBLIC_*`: main API tables in the `public` schema
- `PAYMENT_GATEWAY_*`: payment service tables in the `payment_gateway` schema
- `FULFILLMENT_SERVICE_*`: fulfillment service tables in the `fulfillment_service` schema
- `NOTIFICATION_SERVICE_*`: notification service tables in the `notification_service` schema

Relationship legend:

- Solid lines: direct database relationships inside the owning schema
- Dotted lines: logical or message-driven cross-schema references, not enforced by PostgreSQL foreign keys

## Table Summary

### public schema

- `Items`: product catalog
- `WarehouseInventories`: per-item stock and reservation counts by warehouse
- `Baskets`: one basket per user session
- `BasketItems`: basket line items linked to baskets and catalog items
- `DeliveryOptions`: shipping methods used during checkout
- `PaymentOptions`: payment methods used during checkout
- `Orders`: placed orders with payment and fulfillment state
- `OrderItems`: order line items
- `InboxMessages`: idempotency store for consumed inbound integration messages
- `OutboxMessages`: pending and published integration events from the main API

### payment_gateway schema

- `PaymentAttempts`: idempotent authorization attempts keyed by `IdempotencyKey`

### fulfillment_service schema

- `FulfillmentAttempts`: downstream reservation and shipping workflow state for `order.paid`
- `FulfillmentAttemptItems`: item-level rows belonging to one fulfillment attempt
- `OutboxMessages`: fulfillment progress events waiting to be published

### notification_service schema

- `NotificationUsers`: fake user contact data and channel preferences, seeded with 10 demo users
- `EmailNotificationDeliveries`: one row per handled email notification
- `SmsNotificationDeliveries`: one row per handled SMS notification

## Cross-Schema Notes

- Only the `public` schema currently has direct relational foreign keys between most business tables.
- The other service schemas store downstream projections and workflow history keyed by values such as `OrderId`, `OrderNumber`, `UserId`, or `MessageId` rather than database-enforced cross-schema foreign keys.
- `public.OutboxMessages` is the source of `order.paid` fan-out into fulfillment and notification processing.