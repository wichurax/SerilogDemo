namespace SerilogDemo.Models;

/// <summary>
/// Represents a delivery/shipping option.
/// </summary>
public class DeliveryOption
{
    public Guid Id { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int EstimatedDaysMin { get; set; }
    public int EstimatedDaysMax { get; set; }
    public bool IsActive { get; set; } = true;
}
