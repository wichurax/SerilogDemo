namespace SerilogDemo.DTOs;

public record ItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Category,
    string ImageUrl,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity
);

public record BasketDto(
    Guid Id,
    string UserId,
    List<BasketItemDto> Items,
    decimal TotalPrice,
    int TotalItems,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record BasketItemDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record AddToBasketRequest(
    Guid ItemId,
    int Quantity = 1
);

public record UpdateBasketItemRequest(
    int Quantity
);

public record DeliveryOptionDto(
    Guid Id,
    string CourierName,
    string Name,
    string Description,
    decimal Price,
    int EstimatedDaysMin,
    int EstimatedDaysMax
);

public record PaymentOptionDto(
    Guid Id,
    string Name,
    string Description,
    string Icon
);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    string UserId,
    List<OrderItemDto> Items,
    DeliveryOptionDto DeliveryOption,
    decimal DeliveryPrice,
    PaymentOptionDto PaymentOption,
    decimal ItemsTotal,
    decimal TotalPrice,
    string Status,
    string PaymentStatus,
    Guid? PaymentAttemptId,
    string? PaymentProviderCode,
    string? PaymentFailureReason,
    OrderFulfillmentDto Fulfillment,
    DateTime CreatedAt
);

public record OrderFulfillmentDto(
    string Status,
    string? WarehouseName,
    bool IsPendingDelivery,
    string? TrackingReference,
    string? LastMessage,
    DateTime? DispatchedAtUtc,
    DateTime? LastUpdatedAtUtc
);

public record OrderItemDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record PlaceOrderRequest(
    Guid DeliveryOptionId,
    Guid PaymentOptionId
);

public record OrderSummary(
    Guid OrderId,
    string OrderNumber,
    decimal TotalPrice,
    string Status,
    string FulfillmentStatus,
    bool IsPendingDelivery,
    DateTime CreatedAt
);

public record WarehouseInventoryDto(
    Guid ItemId,
    string ItemName,
    string Category,
    string WarehouseName,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity,
    DateTime UpdatedAtUtc
);

public record AdjustWarehouseInventoryRequest(
    int DeltaQuantity,
    string? Reason
);

public record RestockInventoryRequest(
    int Quantity,
    string? Reason
);

public record WriteOffInventoryRequest(
    int Quantity,
    string? Reason
);

public record RecountInventoryRequest(
    int QuantityOnHand,
    string? Reason
);

public record WarehouseInventoryCommandResultDto(
    string Message,
    int QuantityOnHand,
    int QuantityReserved,
    int AvailableQuantity
);
