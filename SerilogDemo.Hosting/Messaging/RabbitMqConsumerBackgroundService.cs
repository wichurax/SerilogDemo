using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SerilogDemo.Hosting.Messaging;

public abstract class RabbitMqConsumerBackgroundService : BackgroundService
{
    private static readonly TimeSpan DefaultConnectionRetryDelay = TimeSpan.FromSeconds(2);

    private readonly RabbitMqOptions _options;
    private readonly ILogger _logger;
    private readonly string _consumerName;
    private readonly string _exchangeName;
    private readonly string _routingKey;
    private readonly string _queueName;

    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;

    protected RabbitMqConsumerBackgroundService(
        string consumerName,
        string exchangeName,
        string routingKey,
        string queueName,
        RabbitMqOptions options,
        ILogger logger)
    {
        _consumerName = consumerName;
        _exchangeName = exchangeName;
        _routingKey = routingKey;
        _queueName = queueName;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializeConsumerWithRetryAsync(cancellationToken);
            _logger.LogInformation("{ConsumerName} connected to RabbitMQ and waiting for messages.", _consumerName);
            await WaitForShutdownAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CleanupResourcesAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    protected virtual Task ConfigureConsumerChannelAsync(IChannel channel, CancellationToken cancellationToken)
    {
        return channel.BasicQosAsync(0, 1, false, cancellationToken);
    }

    protected abstract Task HandleMessageAsync(object sender, BasicDeliverEventArgs eventArgs, IChannel channel);

    private async Task InitializeConsumerWithRetryAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ConnectAndStartConsumerAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "{ConsumerName} could not connect to RabbitMQ. Retrying in {DelaySeconds} seconds.", _consumerName, DefaultConnectionRetryDelay.TotalSeconds);
                await CleanupResourcesAsync();
                await Task.Delay(DefaultConnectionRetryDelay, cancellationToken);
            }
        }
    }

    private async Task ConnectAndStartConsumerAsync(CancellationToken cancellationToken)
    {
        var factory = RabbitMqConnectionFactoryFactory.Create(_options);

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopologyInitializer.DeclareExchangeAndBoundQueueAsync(_channel, _exchangeName, _routingKey, _queueName, cancellationToken);
        await ConfigureConsumerChannelAsync(_channel, cancellationToken);

        var channel = _channel;
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (sender, eventArgs) => HandleMessageAsync(sender, eventArgs, channel);
        _consumerTag = await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer, cancellationToken);
    }

    private async Task CleanupResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (_channel is not null && _consumerTag is not null)
        {
            try
            {
                await _channel.BasicCancelAsync(_consumerTag, noWait: false, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogDebug(exception, "{ConsumerName} channel cancellation during cleanup failed.", _consumerName);
            }
        }

        _consumerTag = null;

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        _connection?.Dispose();
        _connection = null;
    }

    private static Task WaitForShutdownAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}