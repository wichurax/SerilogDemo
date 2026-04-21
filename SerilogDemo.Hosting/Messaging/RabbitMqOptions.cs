namespace SerilogDemo.Hosting.Messaging;

/// <summary>
/// Represents RabbitMQ connection and publishing settings shared by hosted services.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Gets the configuration section name for RabbitMQ settings.
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// Gets or sets the RabbitMQ host name.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ TCP port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the RabbitMQ user name.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets a value indicating whether background publishers should send messages.
    /// </summary>
    public bool PublishEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay between publisher polling iterations in seconds.
    /// </summary>
    public int PublishIntervalSeconds { get; set; } = 3;
}