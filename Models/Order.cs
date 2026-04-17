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
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public OrderFulfillmentStatus FulfillmentStatus { get; set; } = OrderFulfillmentStatus.Pending;
    public string? FulfillmentWarehouse { get; set; }
    public string? FulfillmentTrackingReference { get; set; }
    public string? FulfillmentLastMessage { get; set; }
    public DateTime? FulfillmentDispatchedAtUtc { get; set; }
    public DateTime? FulfillmentLastUpdatedAtUtc { get; set; }
    public Guid? PaymentAttemptId { get; set; }
    public string? PaymentProviderCode { get; set; }
    public string? PaymentFailureReason { get; set; }
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

public enum PaymentStatus
{
    Pending,
    Authorized,
    Declined,
    TimedOut
}

public enum OrderFulfillmentStatus
{
    Pending,
    Reserved,
    Collected,
    Packed,
    Shipped,
    Failed
}
