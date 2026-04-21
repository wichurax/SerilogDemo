using RabbitMQ.Client;

namespace SerilogDemo.Hosting.Messaging;

/// <summary>
/// Declares RabbitMQ exchanges and binds durable queues used by the application.
/// </summary>
public static class RabbitMqTopologyInitializer
{
    /// <summary>
    /// Declares a topic exchange and binds a single durable queue to it.
    /// </summary>
    public static async Task DeclareExchangeAndBoundQueueAsync(
        IChannel channel,
        string exchangeName,
        string routingKey,
        string queueName,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, exchangeName, routingKey, arguments: null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Declares a topic exchange and binds multiple durable queues to it.
    /// </summary>
    public static async Task DeclareExchangeAndBoundQueuesAsync(
        IChannel channel,
        string exchangeName,
        string routingKey,
        IEnumerable<string> queueNames,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

        foreach (var queueName in queueNames)
        {
            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queueName, exchangeName, routingKey, arguments: null, cancellationToken: cancellationToken);
        }
    }
}