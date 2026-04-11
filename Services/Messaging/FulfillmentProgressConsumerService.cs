using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SerilogDemo.Data;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;
using SerilogDemo.Models;
using SerilogDemo.Services.Inventory;
using SerilogDemo.Telemetry;

namespace SerilogDemo.Services.Messaging;

public sealed class FulfillmentProgressConsumerService : RabbitMqConsumerBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<FulfillmentProgressConsumerService> _logger;

    public FulfillmentProgressConsumerService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ILogger<FulfillmentProgressConsumerService> logger)
        : base(
            consumerName: "Order fulfillment projection consumer",
            exchangeName: MessagingTopology.OrderEventsExchange,
            routingKey: MessagingTopology.FulfillmentProgressRoutingKey,
            queueName: MessagingTopology.OrderProjectionQueueName,
            options: rabbitMqOptions.Value,
            logger: logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(object sender, BasicDeliverEventArgs eventArgs, IChannel channel)
    {
        var parentContext = TraceContextPropagation.Extract(eventArgs.BasicProperties.Headers);
        using var activity = parentContext != default
            ? EcommerceDiagnostics.ActivitySource.StartActivity("fulfillment.project_progress", ActivityKind.Consumer, parentContext)
            : EcommerceDiagnostics.ActivitySource.StartActivity("fulfillment.project_progress", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", MessagingTopology.OrderProjectionQueueName);
        activity?.SetTag("messaging.rabbitmq.routing_key", eventArgs.RoutingKey);

        var messageId = eventArgs.BasicProperties.MessageId ?? $"delivery-{eventArgs.DeliveryTag}";
        activity?.SetTag("message.id", messageId);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

            if (await dbContext.InboxMessages.AnyAsync(message => message.MessageId == messageId))
            {
                activity?.SetTag("fulfillment.outcome", "duplicate_ignored");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            var payload = JsonSerializer.Deserialize<FulfillmentProgressIntegrationEvent>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
            if (payload is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Empty fulfillment progress payload.");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            if (!Enum.TryParse<OrderFulfillmentStatus>(payload.Status, true, out var fulfillmentStatus))
            {
                _logger.LogWarning("Ignoring fulfillment progress event {EventId} with unknown status {Status}", payload.EventId, payload.Status);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var order = await dbContext.Orders
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == payload.OrderId);

            if (order is null)
            {
                _logger.LogWarning("Ignoring fulfillment progress for missing order {OrderId}", payload.OrderId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            activity?.SetTag("order.id", order.Id);
            activity?.SetTag("order.number", order.OrderNumber);
            activity?.SetTag("fulfillment.status", fulfillmentStatus.ToString());

            if (fulfillmentStatus >= order.FulfillmentStatus)
            {
                order.FulfillmentStatus = fulfillmentStatus;
                order.FulfillmentWarehouse = payload.Warehouse;
                order.FulfillmentTrackingReference = payload.TrackingReference;
                order.FulfillmentLastMessage = payload.Message;
                order.FulfillmentLastUpdatedAtUtc = payload.OccurredAtUtc.UtcDateTime;

                if (fulfillmentStatus == OrderFulfillmentStatus.Shipped)
                {
                    order.Status = OrderStatus.Shipped;
                    order.FulfillmentDispatchedAtUtc = payload.DispatchedAtUtc?.UtcDateTime ?? payload.OccurredAtUtc.UtcDateTime;

                    var deductionResult = await inventoryService.DeductReservedStockAsync(
                        order.Items
                            .Select(item => new InventoryQuantityChange(item.ItemId, item.ItemName, item.Quantity))
                            .ToArray(),
                        CancellationToken.None);

                    if (!deductionResult.Succeeded)
                    {
                        throw new InvalidOperationException(deductionResult.Message);
                    }
                }
                else if (fulfillmentStatus != OrderFulfillmentStatus.Pending && order.Status is OrderStatus.Confirmed or OrderStatus.Pending)
                {
                    order.Status = OrderStatus.Processing;
                }
            }

            dbContext.InboxMessages.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                Type = nameof(FulfillmentProgressIntegrationEvent),
                ProcessedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            EcommerceMetrics.FulfillmentCallbacks.Add(1, new TagList
            {
                { "status", fulfillmentStatus.ToString().ToLowerInvariant() }
            });

            activity?.SetTag("fulfillment.outcome", "projected");
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Failed to project fulfillment progress message {MessageId}", messageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, true);
        }
    }
}