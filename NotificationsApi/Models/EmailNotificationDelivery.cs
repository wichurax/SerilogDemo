namespace NotificationsApi.Models;

public sealed class EmailNotificationDelivery
{
    public Guid Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string RecipientEmail { get; set; } = string.Empty;

    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Sent;

    public string? FailureReason { get; set; }

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}