using System.Diagnostics;
using NotificationsApi.Telemetry;

namespace NotificationsApi.Services;

public interface ISmsNotificationService
{
    Task SendAsync(SmsNotificationPayload payload, CancellationToken cancellationToken);
}

public sealed class FakeSmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<FakeSmsNotificationService> _logger;

    public FakeSmsNotificationService(ILogger<FakeSmsNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(SmsNotificationPayload payload, CancellationToken cancellationToken)
    {
        using var activity = NotificationDiagnostics.ActivitySource.StartActivity("notification.sms.fake_send", ActivityKind.Internal);
        activity?.SetTag("notification.channel", "sms");
        activity?.SetTag("notification.provider", "fake");
        activity?.SetTag("notification.transport", "log_only");
        activity?.SetTag("notification.destination", payload.RecipientPhoneNumber);
        activity?.SetTag("order.number", payload.OrderNumber);
        activity?.SetTag("app.user_id", payload.UserId);
        activity?.SetTag("notification.payload_length", payload.Message.Length);

        try
        {
            _logger.LogInformation(
                "Fake SMS notification prepared for order {OrderNumber} to {RecipientPhoneNumber}. Message: {Message}",
                payload.OrderNumber,
                payload.RecipientPhoneNumber,
                payload.Message);

            activity?.AddEvent(new ActivityEvent("notification.payload_logged"));
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }
}