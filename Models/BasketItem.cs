namespace SerilogDemo.Models;

/// <summary>
/// Represents an item in a shopping basket.
/// </summary>
public class BasketItem
{
    public Guid Id { get; set; }
    public Guid BasketId { get; set; }
    public Basket Basket { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
