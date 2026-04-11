namespace SerilogDemo.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string RoutingKey { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAtUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}