namespace FulfillmentService.Models;

public sealed record FulfillmentAttemptDto(
    Guid OrderId,
    string OrderNumber,
    string UserId,
    string Warehouse,
    string DeliveryCourier,
    string DeliveryOptionName,
    string Status,
    string? TrackingReference,
    DateTime CreatedAtUtc,
    DateTime LastProcessedAtUtc,
    DateTime? CollectedAtUtc,
    DateTime? PackedAtUtc,
    DateTime? ShippedAtUtc,
    IReadOnlyCollection<FulfillmentAttemptItemDto> Items);

public sealed record FulfillmentAttemptItemDto(
    Guid ItemId,
    string ItemName,
    int Quantity);

public sealed record FulfillmentActionRequest(string? Message);

public sealed record ShipFulfillmentRequest(string? TrackingReference, string? Message);