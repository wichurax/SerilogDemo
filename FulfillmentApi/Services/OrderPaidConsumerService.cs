using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FulfillmentApi.Data;
using FulfillmentApi.Models;
using FulfillmentApi.Options;
using FulfillmentApi.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Hosting.Observability;
using SerilogDemo.Messaging;

namespace FulfillmentApi.Services;

public sealed class OrderPaidConsumerService : RabbitMqConsumerBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly FulfillmentOptions _fulfillmentOptions;
    private readonly ILogger<OrderPaidConsumerService> _logger;

    public OrderPaidConsumerService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<FulfillmentOptions> fulfillmentOptions,
        ILogger<OrderPaidConsumerService> logger)
        : base(
            consumerName: "Fulfillment consumer",
            exchangeName: MessagingTopology.OrderEventsExchange,
            routingKey: MessagingTopology.OrderPaidRoutingKey,
            queueName: MessagingTopology.FulfillmentQueueName,
            options: rabbitMqOptions.Value,
            logger: logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _fulfillmentOptions = fulfillmentOptions.Value;
        _logger = logger;
    }

    protected override Task ConfigureConsumerChannelAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var prefetchCount = (ushort)Math.Clamp(_fulfillmentOptions.ConsumerPrefetchCount, 1, ushort.MaxValue);
        return channel.BasicQosAsync(0, prefetchCount, false, cancellationToken);
    }

    protected override async Task HandleMessageAsync(object sender, BasicDeliverEventArgs eventArgs, IChannel channel)
    {
        var parentContext = TraceContextPropagation.Extract(eventArgs.BasicProperties.Headers);
        using var activity = parentContext != default
            ? FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.consume_order_paid", ActivityKind.Consumer, parentContext)
            : FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.consume_order_paid", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", MessagingTopology.FulfillmentQueueName);
        activity?.SetTag("messaging.rabbitmq.routing_key", eventArgs.RoutingKey);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.rabbitmq.redelivered", eventArgs.Redelivered);

        var messageId = eventArgs.BasicProperties.MessageId ?? $"delivery-{eventArgs.DeliveryTag}";
        activity?.SetTag("message.id", messageId);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();

            var payloadText = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            OrderPaidIntegrationEvent? payload;
            try
            {
                payload = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(payloadText);
            }
            catch (JsonException)
            {
                payload = null;
            }

            if (payload is null || payload.Delivery is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Order paid payload is invalid.");
                _logger.LogWarning(
                    "Fulfillment consumer skipped malformed order-paid payload for message {MessageId}. Payload: {Payload}",
                    messageId,
                    payloadText);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            using var orderScope = BusinessLogContext.PushOrder(payload.OrderId, payload.OrderNumber, payload.UserId);
            activity?.SetTag("order.id", payload.OrderId);
            activity?.SetTag("order.number", payload.OrderNumber);
            activity?.SetTag("fulfillment.warehouse", _fulfillmentOptions.WarehouseName);
            activity?.SetTag("delivery.courier", payload.Delivery.CourierName);
            activity?.SetTag("messaging.rabbitmq.prefetch_count", _fulfillmentOptions.ConsumerPrefetchCount);

            var existingAttempt = await dbContext.FulfillmentAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.MessageId == messageId);

            if (existingAttempt is not null)
            {
                activity?.SetTag("fulfillment.outcome", "duplicate_ignored");
                activity?.SetTag("fulfillment.status", existingAttempt.Status.ToString());
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            var attempt = new FulfillmentAttempt
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                OrderId = payload.OrderId,
                OrderNumber = payload.OrderNumber,
                UserId = payload.UserId,
                Warehouse = _fulfillmentOptions.WarehouseName,
                DeliveryCourier = payload.Delivery.CourierName,
                DeliveryOptionName = payload.Delivery.Name,
                CreatedAtUtc = DateTime.UtcNow
            };

            attempt.AttemptCount += 1;
            attempt.LastProcessedAtUtc = DateTime.UtcNow;
            activity?.SetTag("fulfillment.attempt_count", attempt.AttemptCount);

            attempt.Status = FulfillmentStatus.Reserved;
            attempt.LastError = null;

            attempt.Items = payload.Items
                .Select(item => new FulfillmentAttemptItem
                {
                    Id = Guid.NewGuid(),
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity
                })
                .ToList();

            dbContext.FulfillmentAttempts.Add(attempt);

            dbContext.OutboxMessages.Add(FulfillmentProgressOutboxFactory.Create(attempt, "Inventory reserved for fulfillment.", activity));

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                var duplicateAttempt = await dbContext.FulfillmentAttempts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.MessageId == messageId);

                if (duplicateAttempt is not null)
                {
                    activity?.SetTag("fulfillment.outcome", "duplicate_ignored");
                    activity?.SetTag("fulfillment.status", duplicateAttempt.Status.ToString());
                    _logger.LogInformation(
                        "Fulfillment message {MessageId} for order {OrderNumber} was already persisted by another consumer instance.",
                        messageId,
                        payload.OrderNumber);
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    return;
                }

                throw new InvalidOperationException($"Failed to persist fulfillment attempt for message {messageId}.", exception);
            }

            activity?.SetTag("fulfillment.outcome", "reserved");

            _logger.LogInformation(
                "Fulfillment reserved stock for order {OrderNumber} in warehouse {Warehouse} on attempt {AttemptCount} using message {MessageId}",
                payload.OrderNumber,
                attempt.Warehouse,
                attempt.AttemptCount,
                messageId);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            activity?.SetTag("fulfillment.outcome", "consumer_error");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Fulfillment consumer failed for delivery tag {DeliveryTag}", eventArgs.DeliveryTag);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
        }
    }
}