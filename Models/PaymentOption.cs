namespace SerilogDemo.Models;

/// <summary>
/// Represents a payment method option.
/// </summary>
public class PaymentOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
