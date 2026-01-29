namespace SerilogDemo.Models;

/// <summary>
/// Represents a placed order.
/// </summary>
public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    
    public List<OrderItem> Items { get; set; } = [];
    
    public Guid DeliveryOptionId { get; set; }
    public DeliveryOption DeliveryOption { get; set; } = null!;
    public decimal DeliveryPrice { get; set; }
    
    public Guid PaymentOptionId { get; set; }
    public PaymentOption PaymentOption { get; set; } = null!;
    
    public decimal ItemsTotal { get; set; }
    public decimal TotalPrice { get; set; }
    
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
