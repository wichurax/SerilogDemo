using System.Diagnostics;
using NotificationService.Telemetry;

namespace NotificationService.Services;

public interface IEmailNotificationService
{
    Task SendAsync(EmailNotificationPayload payload, CancellationToken cancellationToken);
}

public sealed class FakeEmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<FakeEmailNotificationService> _logger;

    public FakeEmailNotificationService(ILogger<FakeEmailNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailNotificationPayload payload, CancellationToken cancellationToken)
    {
        using var activity = NotificationDiagnostics.ActivitySource.StartActivity("notification.email.fake_send", ActivityKind.Internal);
        activity?.SetTag("notification.channel", "email");
        activity?.SetTag("notification.provider", "fake");
        activity?.SetTag("notification.transport", "log_only");
        activity?.SetTag("notification.destination", payload.RecipientEmail);
        activity?.SetTag("order.number", payload.OrderNumber);
        activity?.SetTag("app.user_id", payload.UserId);
        activity?.SetTag("notification.subject", payload.Subject);
        activity?.SetTag("notification.payload_length", payload.Body.Length);

        try
        {
            _logger.LogInformation(
                "Fake email notification prepared for order {OrderNumber} to {RecipientEmail}. Subject: {Subject}. Body: {Body}",
                payload.OrderNumber,
                payload.RecipientEmail,
                payload.Subject,
                payload.Body);

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