using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Options;
using NotificationService.Telemetry;
using RabbitMQ.Client.Events;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;

namespace NotificationService.Services;

public sealed class OrderPaidConsumerService : RabbitMqConsumerBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly NotificationServiceOptions _notificationOptions;
    private readonly ILogger<OrderPaidConsumerService> _logger;

    public OrderPaidConsumerService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<NotificationServiceOptions> notificationOptions,
        ILogger<OrderPaidConsumerService> logger)
        : base(
            consumerName: "Notification consumer",
            exchangeName: MessagingTopology.OrderEventsExchange,
            routingKey: MessagingTopology.OrderPaidRoutingKey,
            queueName: MessagingTopology.NotificationQueueName,
            options: rabbitMqOptions.Value,
            logger: logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _notificationOptions = notificationOptions.Value;
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
            ? NotificationDiagnostics.ActivitySource.StartActivity("notification.consume_order_paid", ActivityKind.Consumer, parentContext)
            : NotificationDiagnostics.ActivitySource.StartActivity("notification.consume_order_paid", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", MessagingTopology.NotificationQueueName);
        activity?.SetTag("messaging.rabbitmq.routing_key", eventArgs.RoutingKey);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.rabbitmq.redelivered", eventArgs.Redelivered);

        var messageId = eventArgs.BasicProperties.MessageId ?? $"delivery-{eventArgs.DeliveryTag}";
        activity?.SetTag("message.id", messageId);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var payload = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
            if (payload is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Order paid payload was empty.");
                _logger.LogWarning("Notification consumer received an empty order-paid payload for message {MessageId}", messageId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            activity?.SetTag("order.id", payload.OrderId);
            activity?.SetTag("order.number", payload.OrderNumber);
            activity?.SetTag("notification.simulation_mode", _notificationOptions.SimulationMode.ToString());

            var attempt = await dbContext.NotificationAttempts.SingleOrDefaultAsync(item => item.MessageId == messageId);

            if (attempt is not null && attempt.Status == NotificationDeliveryStatus.Sent)
            {
                activity?.SetTag("notification.outcome", "duplicate_ignored");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            attempt ??= new NotificationAttempt
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                OrderId = payload.OrderId,
                OrderNumber = payload.OrderNumber,
                UserId = payload.UserId,
                Channel = "email"
            };

            attempt.AttemptCount += 1;
            attempt.LastProcessedAtUtc = DateTime.UtcNow;
            activity?.SetTag("notification.attempt_count", attempt.AttemptCount);

            var failFirstAttempt = _notificationOptions.SimulationMode == NotificationSimulationMode.FailFirstAttempt
                && attempt.AttemptCount == 1;

            if (failFirstAttempt)
            {
                attempt.Status = NotificationDeliveryStatus.RetryScheduled;
                attempt.LastError = "Simulated notification transport failure on first attempt.";

                if (dbContext.Entry(attempt).State == EntityState.Detached)
                {
                    dbContext.NotificationAttempts.Add(attempt);
                }

                await dbContext.SaveChangesAsync();

                activity?.SetTag("notification.outcome", "retry_scheduled");
                activity?.SetStatus(ActivityStatusCode.Error, attempt.LastError);

                _logger.LogWarning(
                    "Notification attempt for order {OrderNumber} failed intentionally on attempt {AttemptCount}; requeueing message {MessageId}",
                    payload.OrderNumber,
                    attempt.AttemptCount,
                    messageId);

                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            attempt.Status = NotificationDeliveryStatus.Sent;
            attempt.LastError = null;

            if (dbContext.Entry(attempt).State == EntityState.Detached)
            {
                dbContext.NotificationAttempts.Add(attempt);
            }

            await dbContext.SaveChangesAsync();

            activity?.SetTag("notification.outcome", "sent");

            _logger.LogInformation(
                "Notification sent for order {OrderNumber} on attempt {AttemptCount} using message {MessageId}",
                payload.OrderNumber,
                attempt.AttemptCount,
                messageId);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            activity?.SetTag("notification.outcome", "consumer_error");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Notification consumer failed for delivery tag {DeliveryTag}", eventArgs.DeliveryTag);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
        }
    }
}