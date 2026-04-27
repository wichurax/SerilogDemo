using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentGateway.Contracts;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Models;
using SerilogDemo.Services.Checkout;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ICheckoutService _checkoutService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(EcommerceDbContext context, ICheckoutService checkoutService, ILogger<OrdersController> logger)
    {
        _context = context;
        _checkoutService = checkoutService;
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
                o.FulfillmentStatus.ToString(),
                o.FulfillmentStatus == OrderFulfillmentStatus.Shipped,
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
            .AsNoTracking()
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
            .AsNoTracking()
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

        var paymentScenario = ParsePaymentScenario(Request.Headers["X-Payment-Scenario"].FirstOrDefault());
        var checkoutResult = await _checkoutService.PlaceOrderAsync(userId, request, paymentScenario, HttpContext.RequestAborted);

        if (checkoutResult.Outcome == CheckoutOutcome.ValidationFailed)
        {
            return BadRequest(new { message = checkoutResult.Message });
        }

        var order = checkoutResult.Order!;
        var dto = MapToDto(order);

        return checkoutResult.Outcome switch
        {
            CheckoutOutcome.Authorized => CreatedAtAction(nameof(GetOrder), new { id = order.Id }, dto),
            CheckoutOutcome.Declined => Conflict(new
            {
                message = checkoutResult.Message,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                paymentStatus = order.PaymentStatus.ToString(),
                paymentProviderCode = order.PaymentProviderCode
            }),
            CheckoutOutcome.TimedOut => StatusCode(StatusCodes.Status202Accepted, new
            {
                message = checkoutResult.Message,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                paymentStatus = order.PaymentStatus.ToString(),
                paymentProviderCode = order.PaymentProviderCode
            }),
            _ => BadRequest(new { message = checkoutResult.Message })
        };
    }

    private static OrderDto MapToDto(Order order) => 
        new(
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
            order.PaymentStatus.ToString(),
            order.PaymentAttemptId,
            order.PaymentProviderCode,
            order.PaymentFailureReason,
            new OrderFulfillmentDto(
                order.FulfillmentStatus.ToString(),
                order.FulfillmentWarehouse,
                order.FulfillmentStatus == OrderFulfillmentStatus.Shipped,
                order.FulfillmentTrackingReference,
                order.FulfillmentLastMessage,
                order.FulfillmentCollectedAtUtc,
                order.FulfillmentPackedAtUtc,
                order.FulfillmentDispatchedAtUtc,
                order.FulfillmentLastUpdatedAtUtc),
            order.CreatedAt
        );

    private static PaymentScenario? ParsePaymentScenario(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<PaymentScenario>(value, ignoreCase: true, out var scenario)
            ? scenario
            : null;
    }
}
