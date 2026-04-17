using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        FulfillmentProgressIntegrationEvent? payload = null;
        OrderFulfillmentStatus fulfillmentStatus = default;
        var hasFulfillmentStatus = false;
        IDbContextTransaction? transaction = null;

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

            payload = JsonSerializer.Deserialize<FulfillmentProgressIntegrationEvent>(Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
            if (payload is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Empty fulfillment progress payload.");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            if (!Enum.TryParse<OrderFulfillmentStatus>(payload.Status, true, out var parsedFulfillmentStatus))
            {
                _logger.LogWarning("Ignoring fulfillment progress event {EventId} with unknown status {Status}", payload.EventId, payload.Status);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            fulfillmentStatus = parsedFulfillmentStatus;
            hasFulfillmentStatus = true;
            activity?.SetTag("fulfillment.incoming_status", fulfillmentStatus.ToString());

            transaction = await dbContext.Database.BeginTransactionAsync();
            var order = await LoadOrderForUpdateAsync(dbContext, payload.OrderId, CancellationToken.None);

            if (order is null)
            {
                _logger.LogWarning("Ignoring fulfillment progress for missing order {OrderId}", payload.OrderId);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            if (await dbContext.InboxMessages.AnyAsync(message => message.MessageId == messageId))
            {
                activity?.SetTag("fulfillment.outcome", "duplicate_ignored_after_lock");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            activity?.SetTag("order.id", order.Id);
            activity?.SetTag("order.number", order.OrderNumber);
            activity?.SetTag("fulfillment.status", fulfillmentStatus.ToString());

            var currentFulfillmentStatus = order.FulfillmentStatus;
            activity?.SetTag("fulfillment.current_status", currentFulfillmentStatus.ToString());
            if (ShouldProjectStatus(currentFulfillmentStatus, fulfillmentStatus))
            {
                order.FulfillmentStatus = fulfillmentStatus;
                order.FulfillmentWarehouse = payload.Warehouse;
                order.FulfillmentTrackingReference = string.IsNullOrWhiteSpace(payload.TrackingReference)
                    ? null
                    : payload.TrackingReference;
                order.FulfillmentLastMessage = payload.Message;
                order.FulfillmentLastUpdatedAtUtc = payload.OccurredAtUtc.UtcDateTime;

                if (fulfillmentStatus == OrderFulfillmentStatus.Shipped)
                {
                    order.Status = OrderStatus.Shipped;
                    order.FulfillmentDispatchedAtUtc = payload.DispatchedAtUtc?.UtcDateTime ?? payload.OccurredAtUtc.UtcDateTime;

                    if (currentFulfillmentStatus != OrderFulfillmentStatus.Shipped)
                    {
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
                }
                else if (fulfillmentStatus == OrderFulfillmentStatus.Failed)
                {
                    order.Status = OrderStatus.Cancelled;
                    order.FulfillmentDispatchedAtUtc = null;

                    if (currentFulfillmentStatus != OrderFulfillmentStatus.Failed)
                    {
                        var releaseResult = await inventoryService.ReleaseReservationAsync(
                            order.Items
                                .Select(item => new InventoryQuantityChange(item.ItemId, item.ItemName, item.Quantity))
                                .ToArray(),
                            CancellationToken.None);

                        if (!releaseResult.Succeeded)
                        {
                            throw new InvalidOperationException(releaseResult.Message);
                        }
                    }
                }
                else if (fulfillmentStatus != OrderFulfillmentStatus.Pending && order.Status is OrderStatus.Confirmed or OrderStatus.Pending)
                {
                    order.Status = OrderStatus.Processing;
                }
            }
            else
            {
                activity?.SetTag("fulfillment.outcome", "ignored_out_of_order");
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
            transaction = null;

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

            if (IsTerminalProjectionFailure(exception))
            {
                activity?.SetTag("fulfillment.outcome", "terminal_failure_acked");
                _logger.LogWarning(
                    exception,
                    "Acking terminal fulfillment projection failure for message {MessageId}, order {OrderId}, incoming status {IncomingStatus}",
                    messageId,
                    payload?.OrderId,
                    hasFulfillmentStatus ? fulfillmentStatus.ToString() : payload?.Status ?? "unknown");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                return;
            }

            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception rollbackException)
                {
                    _logger.LogWarning(rollbackException, "Rollback failed while projecting fulfillment progress message {MessageId}", messageId);
                }
            }

            _logger.LogError(exception, "Failed to project fulfillment progress message {MessageId}", messageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, true);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static Task<Order?> LoadOrderForUpdateAsync(EcommerceDbContext dbContext, Guid orderId, CancellationToken cancellationToken)
    {
        return dbContext.Orders
            .FromSqlInterpolated($@"
                SELECT *
                FROM ""Orders""
                WHERE ""Id"" = {orderId}
                FOR UPDATE")
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    private static bool IsTerminalProjectionFailure(Exception exception)
    {
        return exception is InvalidOperationException invalidOperationException
            && (invalidOperationException.Message.StartsWith("Failed to deduct reserved stock", StringComparison.Ordinal)
                || invalidOperationException.Message.StartsWith("Failed to release reserved stock", StringComparison.Ordinal));
    }

    private static bool ShouldProjectStatus(OrderFulfillmentStatus currentStatus, OrderFulfillmentStatus incomingStatus) => currentStatus switch
    {
        OrderFulfillmentStatus.Pending => true,
        OrderFulfillmentStatus.Reserved => incomingStatus is OrderFulfillmentStatus.Reserved
            or OrderFulfillmentStatus.Collected
            or OrderFulfillmentStatus.Packed
            or OrderFulfillmentStatus.Shipped
            or OrderFulfillmentStatus.Failed,
        OrderFulfillmentStatus.Collected => incomingStatus is OrderFulfillmentStatus.Collected
            or OrderFulfillmentStatus.Packed
            or OrderFulfillmentStatus.Shipped
            or OrderFulfillmentStatus.Failed,
        OrderFulfillmentStatus.Packed => incomingStatus is OrderFulfillmentStatus.Packed
            or OrderFulfillmentStatus.Shipped
            or OrderFulfillmentStatus.Failed,
        OrderFulfillmentStatus.Shipped => incomingStatus == OrderFulfillmentStatus.Shipped,
        OrderFulfillmentStatus.Failed => incomingStatus == OrderFulfillmentStatus.Failed,
        _ => false
    };
}