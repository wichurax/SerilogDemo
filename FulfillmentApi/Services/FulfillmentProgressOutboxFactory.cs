using System.Diagnostics;
using System.Text.Json;
using FulfillmentApi.Models;
using SerilogDemo.Messaging;

namespace FulfillmentApi.Services;

internal static class FulfillmentProgressOutboxFactory
{
    public static OutboxMessage Create(FulfillmentAttempt attempt, string? message, Activity? activity)
    {
        var progressEvent = new FulfillmentProgressIntegrationEvent(
            EventId: Guid.NewGuid(),
            OrderId: attempt.OrderId,
            OrderNumber: attempt.OrderNumber,
            Status: attempt.Status.ToString(),
            Warehouse: attempt.Warehouse,
            DeliveryCourier: attempt.DeliveryCourier,
            TrackingReference: attempt.TrackingReference,
            Message: message,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            CollectedAtUtc: attempt.CollectedAtUtc.HasValue ? new DateTimeOffset(attempt.CollectedAtUtc.Value) : null,
            PackedAtUtc: attempt.PackedAtUtc.HasValue ? new DateTimeOffset(attempt.PackedAtUtc.Value) : null,
            DispatchedAtUtc: attempt.ShippedAtUtc.HasValue ? new DateTimeOffset(attempt.ShippedAtUtc.Value) : null);

        return new OutboxMessage
        {
            Id = progressEvent.EventId,
            Type = nameof(FulfillmentProgressIntegrationEvent),
            RoutingKey = MessagingTopology.FulfillmentProgressRoutingKey,
            Payload = JsonSerializer.Serialize(progressEvent),
            TraceParent = activity?.Id ?? Activity.Current?.Id,
            TraceState = activity?.TraceStateString ?? Activity.Current?.TraceStateString,
            OccurredAtUtc = progressEvent.OccurredAtUtc.UtcDateTime
        };
    }
}