namespace FulfillmentService.Models;

public class FulfillmentAttemptItem
{
    public Guid Id { get; set; }

    public Guid FulfillmentAttemptId { get; set; }

    public FulfillmentAttempt FulfillmentAttempt { get; set; } = null!;

    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}