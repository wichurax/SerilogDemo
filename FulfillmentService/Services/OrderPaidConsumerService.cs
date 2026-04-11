using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FulfillmentService.Data;
using FulfillmentService.Models;
using FulfillmentService.Options;
using FulfillmentService.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;

namespace FulfillmentService.Services;

public sealed class OrderPaidConsumerService : RabbitMqConsumerBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly FulfillmentServiceOptions _fulfillmentOptions;
    private readonly ILogger<OrderPaidConsumerService> _logger;

    public OrderPaidConsumerService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<FulfillmentServiceOptions> fulfillmentOptions,
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
        return channel.BasicQosAsync(0, 1, false, cancellationToken);
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

            var payload = JsonSerializer.Deserialize<OrderPaidIntegrationEvent>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
            if (payload is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Order paid payload was empty.");
                _logger.LogWarning("Fulfillment consumer received an empty order-paid payload for message {MessageId}", messageId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            activity?.SetTag("order.id", payload.OrderId);
            activity?.SetTag("order.number", payload.OrderNumber);
            activity?.SetTag("fulfillment.warehouse", _fulfillmentOptions.WarehouseName);
            activity?.SetTag("delivery.courier", payload.Delivery.CourierName);

            var attempt = await dbContext.FulfillmentAttempts.SingleOrDefaultAsync(item => item.MessageId == messageId);
            if (attempt is not null && attempt.Status == FulfillmentStatus.Reserved)
            {
                activity?.SetTag("fulfillment.outcome", "duplicate_ignored");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            attempt ??= new FulfillmentAttempt
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

            if (_fulfillmentOptions.ProcessingDelayMilliseconds > 0)
            {
                await Task.Delay(_fulfillmentOptions.ProcessingDelayMilliseconds);
            }

            attempt.Status = FulfillmentStatus.Reserved;
            attempt.LastError = null;

            if (attempt.Items.Count == 0)
            {
                attempt.Items = payload.Items
                    .Select(item => new FulfillmentAttemptItem
                    {
                        Id = Guid.NewGuid(),
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity
                    })
                    .ToList();
            }

            if (dbContext.Entry(attempt).State == EntityState.Detached)
            {
                dbContext.FulfillmentAttempts.Add(attempt);
            }

            dbContext.OutboxMessages.Add(FulfillmentProgressOutboxFactory.Create(attempt, "Inventory reserved for fulfillment.", activity));

            await dbContext.SaveChangesAsync();

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