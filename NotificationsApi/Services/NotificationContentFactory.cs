using System.Globalization;
using System.Text;
using SerilogDemo.Messaging;

namespace NotificationsApi.Services;

public static class NotificationContentFactory
{
    public static EmailNotificationPayload CreateEmail(OrderPaidIntegrationEvent orderPaidEvent, ResolvedNotificationUserProfile profile)
    {
        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Hello {profile.UserId},");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"Your payment for order {orderPaidEvent.OrderNumber} was confirmed on {orderPaidEvent.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC.");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Items:");

        foreach (var item in orderPaidEvent.Items)
        {
            bodyBuilder.AppendLine($"- {item.ItemName} x{item.Quantity} @ {FormatAmount(item.UnitPrice)}");
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine($"Delivery: {orderPaidEvent.Delivery.Name} via {orderPaidEvent.Delivery.CourierName} ({FormatEta(orderPaidEvent.Delivery)})");
        bodyBuilder.AppendLine($"Payment method: {orderPaidEvent.PaymentMethodCode}");
        bodyBuilder.AppendLine($"Total: {FormatAmount(orderPaidEvent.TotalPrice)}");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("This is a fake notification generated for the demo environment.");

        return new EmailNotificationPayload(
            profile.Email,
            $"Order {orderPaidEvent.OrderNumber} payment confirmed",
            bodyBuilder.ToString().TrimEnd(),
            orderPaidEvent.OrderNumber,
            profile.UserId);
    }

    public static SmsNotificationPayload CreateSms(OrderPaidIntegrationEvent orderPaidEvent, ResolvedNotificationUserProfile profile)
    {
        var message = $"Order {orderPaidEvent.OrderNumber} confirmed. Total {FormatAmount(orderPaidEvent.TotalPrice)}. " +
                      $"Delivery {orderPaidEvent.Delivery.Name} via {orderPaidEvent.Delivery.CourierName} in {FormatEta(orderPaidEvent.Delivery)}. Demo SMS only.";

        return new SmsNotificationPayload(
            profile.PhoneNumber,
            message,
            orderPaidEvent.OrderNumber,
            profile.UserId);
    }

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatEta(DeliverySelectionSnapshot delivery) =>
        delivery.EstimatedDaysMin == delivery.EstimatedDaysMax
            ? $"{delivery.EstimatedDaysMin} days"
            : $"{delivery.EstimatedDaysMin}-{delivery.EstimatedDaysMax} days";
}

public sealed record EmailNotificationPayload(
    string RecipientEmail,
    string Subject,
    string Body,
    string OrderNumber,
    string UserId);

public sealed record SmsNotificationPayload(
    string RecipientPhoneNumber,
    string Message,
    string OrderNumber,
    string UserId);