using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Models;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(EcommerceDbContext context, ILogger<OrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetUserId()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("X-User-Id header is required");
        }
        return userId;
    }

    /// <summary>
    /// Get all orders for the current user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderSummary>>> GetOrders()
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Fetching orders for user {UserId}", userId);

        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderSummary(
                o.Id,
                o.OrderNumber,
                o.TotalPrice,
                o.Status.ToString(),
                o.CreatedAt
            ))
            .ToListAsync();

        _logger.LogInformation("Returning {Count} orders for user {UserId}", orders.Count, userId);
        return Ok(orders);
    }

    /// <summary>
    /// Get a specific order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid id)
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Fetching order {OrderId} for user {UserId}", id, userId);

        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryOption)
            .Include(o => o.PaymentOption)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found for user {UserId}", id, userId);
            return NotFound(new { message = $"Order with ID {id} not found" });
        }

        var dto = MapToDto(order);
        _logger.LogInformation("Returning order {OrderId} ({OrderNumber}) for user {UserId}", 
            id, order.OrderNumber, userId);
        return Ok(dto);
    }

    /// <summary>
    /// Get an order by order number.
    /// </summary>
    [HttpGet("by-number/{orderNumber}")]
    public async Task<ActionResult<OrderDto>> GetOrderByNumber(string orderNumber)
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Fetching order by number {OrderNumber} for user {UserId}", orderNumber, userId);

        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryOption)
            .Include(o => o.PaymentOption)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderNumber} not found for user {UserId}", orderNumber, userId);
            return NotFound(new { message = $"Order with number {orderNumber} not found" });
        }

        var dto = MapToDto(order);
        _logger.LogInformation("Returning order {OrderNumber} for user {UserId}", orderNumber, userId);
        return Ok(dto);
    }

    /// <summary>
    /// Place a new order from the current basket.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation(
            "Placing order for user {UserId} with delivery option {DeliveryOptionId} and payment option {PaymentOptionId}",
            userId, request.DeliveryOptionId, request.PaymentOptionId);

        // Get the basket
        var basket = await _context.Baskets
            .Include(b => b.Items)
            .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is null || !basket.Items.Any())
        {
            _logger.LogWarning("Cannot place order for user {UserId}: basket is empty", userId);
            return BadRequest(new { message = "Cannot place order with an empty basket" });
        }

        // Validate delivery option
        var deliveryOption = await _context.DeliveryOptions.FindAsync(request.DeliveryOptionId);
        if (deliveryOption is null || !deliveryOption.IsActive)
        {
            _logger.LogWarning("Invalid delivery option {DeliveryOptionId}", request.DeliveryOptionId);
            return BadRequest(new { message = "Invalid delivery option" });
        }

        // Validate payment option
        var paymentOption = await _context.PaymentOptions.FindAsync(request.PaymentOptionId);
        if (paymentOption is null || !paymentOption.IsActive)
        {
            _logger.LogWarning("Invalid payment option {PaymentOptionId}", request.PaymentOptionId);
            return BadRequest(new { message = "Invalid payment option" });
        }

        // Create the order
        var orderNumber = GenerateOrderNumber();
        var itemsTotal = basket.TotalPrice;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            UserId = userId,
            DeliveryOptionId = deliveryOption.Id,
            DeliveryPrice = deliveryOption.Price,
            PaymentOptionId = paymentOption.Id,
            ItemsTotal = itemsTotal,
            TotalPrice = itemsTotal + deliveryOption.Price,
            Status = OrderStatus.Pending,
            Items = basket.Items.Select(bi => new OrderItem
            {
                Id = Guid.NewGuid(),
                ItemId = bi.ItemId,
                ItemName = bi.ItemName,
                UnitPrice = bi.UnitPrice,
                Quantity = bi.Quantity
            }).ToList()
        };

        _context.Orders.Add(order);

        // Clear the basket
        _context.BasketItems.RemoveRange(basket.Items);
        _context.Baskets.Remove(basket);

        await _context.SaveChangesAsync();

        // Log order details for demo purposes
        _logger.LogInformation(
            "Order placed successfully. OrderId: {OrderId}, OrderNumber: {OrderNumber}, UserId: {UserId}, " +
            "ItemsCount: {ItemsCount}, ItemsTotal: {ItemsTotal:C}, DeliveryOption: {DeliveryOption}, " +
            "DeliveryPrice: {DeliveryPrice:C}, PaymentOption: {PaymentOption}, TotalPrice: {TotalPrice:C}",
            order.Id,
            order.OrderNumber,
            userId,
            order.Items.Count,
            order.ItemsTotal,
            deliveryOption.Name,
            order.DeliveryPrice,
            paymentOption.Name,
            order.TotalPrice);

        // Log each item in the order
        foreach (var item in order.Items)
        {
            _logger.LogInformation(
                "Order {OrderNumber} item: {ItemName}, Quantity: {Quantity}, UnitPrice: {UnitPrice:C}, Total: {Total:C}",
                order.OrderNumber,
                item.ItemName,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice);
        }

        // Reload with navigation properties for response
        order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliveryOption)
            .Include(o => o.PaymentOption)
            .FirstAsync(o => o.Id == order.Id);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, MapToDto(order));
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Random.Shared.Next(1000, 9999);
        return $"ORD-{timestamp}-{random}";
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.Items.Select(i => new OrderItemDto(
                i.Id,
                i.ItemId,
                i.ItemName,
                i.UnitPrice,
                i.Quantity,
                i.TotalPrice
            )).ToList(),
            new DeliveryOptionDto(
                order.DeliveryOption.Id,
                order.DeliveryOption.CourierName,
                order.DeliveryOption.Name,
                order.DeliveryOption.Description,
                order.DeliveryOption.Price,
                order.DeliveryOption.EstimatedDaysMin,
                order.DeliveryOption.EstimatedDaysMax
            ),
            order.DeliveryPrice,
            new PaymentOptionDto(
                order.PaymentOption.Id,
                order.PaymentOption.Name,
                order.PaymentOption.Description,
                order.PaymentOption.Icon
            ),
            order.ItemsTotal,
            order.TotalPrice,
            order.Status.ToString(),
            order.CreatedAt
        );
    }
}
