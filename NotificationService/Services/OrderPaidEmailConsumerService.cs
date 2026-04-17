using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Telemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;

namespace NotificationService.Services;

public sealed class OrderPaidEmailConsumerService : RabbitMqConsumerBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OrderPaidEmailConsumerService> _logger;

    public OrderPaidEmailConsumerService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<OrderPaidEmailConsumerService> logger)
        : base(
            consumerName: "Notification email consumer",
            exchangeName: MessagingTopology.OrderEventsExchange,
            routingKey: MessagingTopology.OrderPaidRoutingKey,
            queueName: MessagingTopology.EmailNotificationQueueName,
            options: rabbitMqOptions.Value,
            logger: logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override Task ConfigureConsumerChannelAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return channel.BasicQosAsync(0, 1, false, cancellationToken);
    }

    protected override async Task HandleMessageAsync(object sender, BasicDeliverEventArgs eventArgs, IChannel channel)
    {
        var parentContext = TraceContextPropagation.Extract(eventArgs.BasicProperties.Headers);
        using var activity = parentContext != default
            ? NotificationDiagnostics.ActivitySource.StartActivity("notification.email.consume_order_paid", ActivityKind.Consumer, parentContext)
            : NotificationDiagnostics.ActivitySource.StartActivity("notification.email.consume_order_paid", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", MessagingTopology.EmailNotificationQueueName);
        activity?.SetTag("messaging.rabbitmq.routing_key", eventArgs.RoutingKey);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.rabbitmq.redelivered", eventArgs.Redelivered);
        activity?.SetTag("notification.channel", "email");

        var messageId = eventArgs.BasicProperties.MessageId ?? $"delivery-{eventArgs.DeliveryTag}";
        activity?.SetTag("message.id", messageId);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var userProfileResolver = scope.ServiceProvider.GetRequiredService<INotificationUserProfileResolver>();
            var emailNotificationService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

            var payload = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
            if (payload is null)
            {
                activity?.SetTag("notification.outcome", "payload_empty");
                activity?.AddEvent(new ActivityEvent("notification.payload_empty"));
                activity?.SetStatus(ActivityStatusCode.Error, "Order paid payload was empty.");
                _logger.LogWarning("Email notification consumer received an empty order-paid payload for message {MessageId}", messageId);
                return;
            }

            activity?.SetTag("order.id", payload.OrderId);
            activity?.SetTag("order.number", payload.OrderNumber);
            activity?.SetTag("app.user_id", payload.UserId);

            var existingDelivery = await dbContext.EmailNotificationDeliveries
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.MessageId == messageId);

            if (existingDelivery is not null)
            {
                activity?.SetTag("notification.outcome", "duplicate_ignored");
                activity?.SetTag("notification.delivery_status", existingDelivery.Status.ToString());
                activity?.AddEvent(new ActivityEvent("notification.delivery.duplicate_ignored"));
                activity?.SetStatus(ActivityStatusCode.Ok);
                return;
            }

            ResolvedNotificationUserProfile resolvedProfile;
            using (var resolveActivity = NotificationDiagnostics.ActivitySource.StartActivity("notification.user_profile.resolve", ActivityKind.Internal))
            {
                resolvedProfile = await userProfileResolver.ResolveAsync(payload.UserId, CancellationToken.None);
                resolveActivity?.SetTag("notification.channel", "email");
                resolveActivity?.SetTag("app.user_id", resolvedProfile.UserId);
                resolveActivity?.SetTag("notification.user_profile.exists_in_db", resolvedProfile.ExistsInDatabase);
                resolveActivity?.SetTag("notification.email_enabled", resolvedProfile.EmailEnabled);
                resolveActivity?.SetTag("notification.sms_enabled", resolvedProfile.SmsEnabled);
                resolveActivity?.SetTag("notification.destination", resolvedProfile.Email);
                resolveActivity?.SetStatus(ActivityStatusCode.Ok);
            }

            activity?.SetTag("notification.user_profile.exists_in_db", resolvedProfile.ExistsInDatabase);
            activity?.SetTag("notification.destination", resolvedProfile.Email);

            if (!resolvedProfile.EmailEnabled)
            {
                dbContext.EmailNotificationDeliveries.Add(new EmailNotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    OrderId = payload.OrderId,
                    OrderNumber = payload.OrderNumber,
                    UserId = payload.UserId,
                    RecipientEmail = resolvedProfile.Email,
                    Status = NotificationDeliveryStatus.Skipped,
                    FailureReason = "Email notifications disabled for user.",
                    ProcessedAtUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();

                activity?.SetTag("notification.outcome", "skipped_disabled");
                activity?.AddEvent(new ActivityEvent("notification.delivery.skipped_disabled"));
                activity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation(
                    "Skipped fake email notification for order {OrderNumber} and user {UserId} because email notifications are disabled.",
                    payload.OrderNumber,
                    payload.UserId);
                return;
            }

            try
            {
                var emailPayload = NotificationContentFactory.CreateEmail(payload, resolvedProfile);
                using var dispatchActivity = NotificationDiagnostics.ActivitySource.StartActivity("notification.email.dispatch", ActivityKind.Internal);
                dispatchActivity?.SetTag("notification.channel", "email");
                dispatchActivity?.SetTag("notification.provider", "fake");
                dispatchActivity?.SetTag("notification.transport", "log_only");
                dispatchActivity?.SetTag("notification.destination", emailPayload.RecipientEmail);
                dispatchActivity?.SetTag("order.number", payload.OrderNumber);
                dispatchActivity?.SetTag("app.user_id", payload.UserId);

                await emailNotificationService.SendAsync(emailPayload, CancellationToken.None);

                dbContext.EmailNotificationDeliveries.Add(new EmailNotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    OrderId = payload.OrderId,
                    OrderNumber = payload.OrderNumber,
                    UserId = payload.UserId,
                    RecipientEmail = emailPayload.RecipientEmail,
                    Status = NotificationDeliveryStatus.Sent,
                    ProcessedAtUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();

                activity?.SetTag("notification.outcome", "sent");
                activity?.AddEvent(new ActivityEvent("notification.delivery.persisted"));
                activity?.SetStatus(ActivityStatusCode.Ok);
                dispatchActivity?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation(
                    "Processed fake email notification for order {OrderNumber} using message {MessageId}",
                    payload.OrderNumber,
                    messageId);
            }
            catch (Exception exception)
            {
                dbContext.EmailNotificationDeliveries.Add(new EmailNotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    OrderId = payload.OrderId,
                    OrderNumber = payload.OrderNumber,
                    UserId = payload.UserId,
                    RecipientEmail = resolvedProfile.Email,
                    Status = NotificationDeliveryStatus.Failed,
                    FailureReason = exception.Message,
                    ProcessedAtUtc = DateTime.UtcNow
                });

                await dbContext.SaveChangesAsync();

                activity?.SetTag("notification.outcome", "failed");
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                _logger.LogError(exception, "Email notification consumer failed while processing message {MessageId}", messageId);
            }
        }
        catch (Exception exception)
        {
            activity?.SetTag("notification.outcome", "consumer_error");
            activity?.AddEvent(new ActivityEvent("notification.consumer_error"));
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Email notification consumer failed for delivery tag {DeliveryTag}", eventArgs.DeliveryTag);
        }
        finally
        {
            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
    }
}