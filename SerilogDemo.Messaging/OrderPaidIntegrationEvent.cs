namespace SerilogDemo.Messaging;

public sealed record OrderPaidIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    string OrderNumber,
    string UserId,
    Guid? PaymentAttemptId,
    string PaymentMethodCode,
    DeliverySelectionSnapshot Delivery,
    IReadOnlyList<OrderPaidLineItem> Items,
    decimal TotalPrice,
    DateTimeOffset OccurredAtUtc);

public sealed record OrderPaidLineItem(
    Guid ItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice);

public sealed record DeliverySelectionSnapshot(
    Guid DeliveryOptionId,
    string CourierName,
    string Name,
    decimal Price,
    int EstimatedDaysMin,
    int EstimatedDaysMax);