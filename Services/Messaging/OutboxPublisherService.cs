using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SerilogDemo.Data;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Messaging;
using SerilogDemo.Telemetry;

namespace SerilogDemo.Services.Messaging;

public sealed class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxPublisherService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_options.PublishEnabled)
        {
            _logger.LogInformation("RabbitMQ publishing is disabled.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await CreateConnectionAsync(cancellationToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
                await DeclareTopologyAsync(channel, cancellationToken);
                await PublishPendingMessagesAsync(channel, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox publisher iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PublishIntervalSeconds)), cancellationToken);
        }
    }

    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = RabbitMqConnectionFactoryFactory.Create(_options);
        return await factory.CreateConnectionAsync(cancellationToken);
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await RabbitMqTopologyInitializer.DeclareExchangeAndBoundQueuesAsync(
            channel,
            MessagingTopology.OrderEventsExchange,
            MessagingTopology.OrderPaidRoutingKey,
            [
                MessagingTopology.EmailNotificationQueueName,
                MessagingTopology.SmsNotificationQueueName,
                MessagingTopology.FulfillmentQueueName
            ],
            cancellationToken);
    }

    private async Task PublishPendingMessagesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        const int batchSize = 20;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT *
                FROM ""OutboxMessages""
                WHERE ""PublishedAtUtc"" IS NULL
                ORDER BY ""OccurredAtUtc""
                FOR UPDATE SKIP LOCKED
                LIMIT {batchSize}")
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var parentContext = TraceContextPropagation.Extract(message.TraceParent, message.TraceState);
            using var activity = parentContext != default
                ? EcommerceDiagnostics.ActivitySource.StartActivity("messaging.publish_outbox", ActivityKind.Producer, parentContext)
                : EcommerceDiagnostics.ActivitySource.StartActivity("messaging.publish_outbox", ActivityKind.Producer);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", MessagingTopology.OrderEventsExchange);
            activity?.SetTag("messaging.rabbitmq.routing_key", message.RoutingKey);
            activity?.SetTag("message.id", message.Id);
            activity?.SetTag("messaging.operation", "publish");

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

                var body = Encoding.UTF8.GetBytes(message.Payload);
                await channel.BasicPublishAsync(MessagingTopology.OrderEventsExchange, message.RoutingKey, mandatory: false, basicProperties: properties, body, cancellationToken);

                message.PublishedAtUtc = DateTime.UtcNow;
                message.Attempts += 1;
                message.LastError = null;

                _logger.LogInformation(
                    "Published outbox message {OutboxMessageId} with routing key {RoutingKey}",
                    message.Id,
                    message.RoutingKey);
            }
            catch (Exception exception)
            {
                message.Attempts += 1;
                message.LastError = exception.Message;
                _logger.LogError(exception, "Failed to publish outbox message {OutboxMessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}