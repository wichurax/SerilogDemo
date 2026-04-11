using RabbitMQ.Client;

namespace SerilogDemo.Hosting.Messaging;

public static class RabbitMqConnectionFactoryFactory
{
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