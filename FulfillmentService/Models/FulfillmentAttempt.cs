namespace FulfillmentService.Models;

public class FulfillmentAttempt
{
    public Guid Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Warehouse { get; set; } = string.Empty;

    public string DeliveryCourier { get; set; } = string.Empty;

    public string DeliveryOptionName { get; set; } = string.Empty;

    public FulfillmentStatus Status { get; set; } = FulfillmentStatus.Pending;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public string? TrackingReference { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CollectedAtUtc { get; set; }

    public DateTime? PackedAtUtc { get; set; }

    public DateTime? ShippedAtUtc { get; set; }

    public DateTime LastProcessedAtUtc { get; set; } = DateTime.UtcNow;

    public List<FulfillmentAttemptItem> Items { get; set; } = [];
}

public enum FulfillmentStatus
{
    Pending,
    Reserved,
    Collected,
    Packed,
    Shipped,
    Failed
}