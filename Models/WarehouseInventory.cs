namespace SerilogDemo.Models;

public class WarehouseInventory
{
    public Guid Id { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public Guid ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public int QuantityOnHand { get; set; }

    public int QuantityReserved { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int AvailableQuantity => QuantityOnHand - QuantityReserved;
}