namespace SerilogDemo.Models;

/// <summary>
/// Represents a shopping basket for a user session.
/// </summary>
public class Basket
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<BasketItem> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal TotalPrice => Items.Sum(i => i.Quantity * i.UnitPrice);
    public int TotalItems => Items.Sum(i => i.Quantity);
}
