namespace SerilogDemo.Models;

public class InboxMessage
{
    public Guid Id { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}