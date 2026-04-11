namespace SerilogDemo.Models;

/// <summary>
/// Represents a product item in the catalog.
/// </summary>
public class Item
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<WarehouseInventory> WarehouseInventories { get; set; } = [];
}
