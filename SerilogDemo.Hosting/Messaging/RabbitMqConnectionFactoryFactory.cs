using RabbitMQ.Client;

namespace SerilogDemo.Hosting.Messaging;

/// <summary>
/// Creates configured RabbitMQ connection factories from bound application options.
/// </summary>
public static class RabbitMqConnectionFactoryFactory
{
    /// <summary>
    /// Builds a RabbitMQ connection factory from the provided options.
    /// </summary>
    public static ConnectionFactory Create(RabbitMqOptions options)
    {
        return new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        };
    }
}