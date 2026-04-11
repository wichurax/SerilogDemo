namespace NotificationService.Models;

public class NotificationAttempt
{
    public Guid Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Channel { get; set; } = "email";

    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTime LastProcessedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum NotificationDeliveryStatus
{
    Pending,
    RetryScheduled,
    Sent
}