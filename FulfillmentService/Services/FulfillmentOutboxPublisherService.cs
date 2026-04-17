using System.Diagnostics;
using System.Text;
using FulfillmentService.Data;
using FulfillmentService.Options;
using FulfillmentService.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;

namespace FulfillmentService.Services;

public sealed class FulfillmentOutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly FulfillmentServiceOptions _fulfillmentOptions;
    private readonly ILogger<FulfillmentOutboxPublisherService> _logger;

    public FulfillmentOutboxPublisherService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> options,
        IOptions<FulfillmentServiceOptions> fulfillmentOptions,
        ILogger<FulfillmentOutboxPublisherService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _fulfillmentOptions = fulfillmentOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_options.PublishEnabled)
        {
            _logger.LogInformation("RabbitMQ publishing is disabled for FulfillmentService.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await RabbitMqConnectionFactoryFactory.Create(_options).CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await RabbitMqTopologyInitializer.DeclareExchangeAndBoundQueueAsync(
                    channel,
                    MessagingTopology.OrderEventsExchange,
                    MessagingTopology.FulfillmentProgressRoutingKey,
                    MessagingTopology.OrderProjectionQueueName,
                    cancellationToken);
                await PublishPendingMessagesAsync(channel, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Fulfillment outbox publisher iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PublishIntervalSeconds)), cancellationToken);
        }
    }

    private async Task PublishPendingMessagesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
        var batchSize = Math.Clamp(_fulfillmentOptions.OutboxBatchSize, 1, 200);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT *
                FROM fulfillment_service.""OutboxMessages""
                WHERE ""PublishedAtUtc"" IS NULL
                ORDER BY ""OccurredAtUtc""
                FOR UPDATE SKIP LOCKED
                LIMIT {batchSize}")
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var parentContext = TraceContextPropagation.Extract(message.TraceParent, message.TraceState);
            using var activity = parentContext != default
                ? FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.publish_outbox", ActivityKind.Producer, parentContext)
                : FulfillmentDiagnostics.ActivitySource.StartActivity("fulfillment.publish_outbox", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", MessagingTopology.OrderEventsExchange);
            activity?.SetTag("messaging.rabbitmq.routing_key", message.RoutingKey);
            activity?.SetTag("message.id", message.Id);

            try
            {
                var headers = TraceContextPropagation.CreateHeaders(activity);
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = message.Id.ToString(),
                    Type = message.Type,
                    CorrelationId = activity?.TraceId.ToString(),
                    Headers = headers.Count == 0 ? null : headers
                };

                await channel.BasicPublishAsync(
                    MessagingTopology.OrderEventsExchange,
                    message.RoutingKey,
                    false,
                    properties,
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);

                message.PublishedAtUtc = DateTime.UtcNow;
                message.Attempts += 1;
                message.LastError = null;
            }
            catch (Exception exception)
            {
                message.Attempts += 1;
                message.LastError = exception.Message;
                _logger.LogError(exception, "Failed to publish fulfillment outbox message {OutboxMessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}