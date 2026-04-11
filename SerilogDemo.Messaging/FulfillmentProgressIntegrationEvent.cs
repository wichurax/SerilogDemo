namespace SerilogDemo.Messaging;

public sealed record FulfillmentProgressIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    string OrderNumber,
    string Status,
    string Warehouse,
    string DeliveryCourier,
    string? TrackingReference,
    string? Message,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? DispatchedAtUtc);