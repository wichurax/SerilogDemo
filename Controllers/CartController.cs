using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Models;
using SerilogDemo.Services.Inventory;
using SerilogDemo.Telemetry;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<CartController> _logger;

    public CartController(EcommerceDbContext context, IInventoryService inventoryService, ILogger<CartController> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
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
    /// Get the current user's cart.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cart request without user ID");
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Fetching cart for user {UserId}", userId);

        var cart = await _context.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is null)
        {
            _logger.LogInformation("No cart found for user {UserId}, returning empty cart", userId);
            return Ok(new CartDto(Guid.Empty, userId, [], 0, 0, DateTime.UtcNow, DateTime.UtcNow));
        }

        var dto = MapToDto(cart);
        _logger.LogInformation("Returning cart for user {UserId} with {ItemCount} items, total: {TotalPrice:C}",
            userId, dto.TotalItems, dto.TotalPrice);
        return Ok(dto);
    }

    /// <summary>
    /// Add an item to the cart.
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddToCart([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
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

        if (request.Quantity <= 0)
        {
            _logger.LogWarning("Rejected cart add for user {UserId}: quantity {Quantity} is invalid", userId, request.Quantity);
            return BadRequest(new { message = "Quantity must be greater than zero" });
        }

        _logger.LogInformation("Adding item {ItemId} (quantity: {Quantity}) to cart for user {UserId}",
            request.ItemId, request.Quantity, userId);

        var item = await _context.Items
            .AsNoTracking()
            .Where(catalogItem => catalogItem.Id == request.ItemId)
            .Select(catalogItem => new
            {
                catalogItem.Id,
                catalogItem.Name,
                catalogItem.Price
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            _logger.LogWarning("Item {ItemId} not found when adding to cart", request.ItemId);
            return NotFound(new { message = $"Item with ID {request.ItemId} not found" });
        }

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart is null)
        {
            _logger.LogInformation("Creating new cart for user {UserId}", userId);
            cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };
            _context.Carts.Add(cart);
        }

        var existingItem = cart.Items.FirstOrDefault(ci => ci.ItemId == request.ItemId);
        var requestedQuantity = (existingItem?.Quantity ?? 0) + request.Quantity;
        var availableQuantity = await GetAvailableQuantityAsync(request.ItemId, cancellationToken);
        if (requestedQuantity > availableQuantity)
        {
            return Conflict(new { message = $"Only {availableQuantity} units are available in the warehouse." });
        }

        if (existingItem is not null)
        {
            existingItem.Quantity += request.Quantity;
            _logger.LogInformation("Updated quantity of item {ItemId} in cart to {Quantity}",
                request.ItemId, existingItem.Quantity);
        }
        else
        {
            var cartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ItemId = item.Id,
                ItemName = item.Name,
                UnitPrice = item.Price,
                Quantity = request.Quantity
            };
            _context.CartItems.Add(cartItem);
            _logger.LogInformation("Added new item {ItemId} ({ItemName}) to cart", item.Id, item.Name);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        cart = await _context.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstAsync(c => c.Id == cart.Id, cancellationToken);

        var dto = MapToDto(cart);
        EcommerceMetrics.CartMutations.Add(1, new TagList
        {
            { "operation", "add" }
        });
        _logger.LogInformation("Cart updated for user {UserId}. Total items: {TotalItems}, Total: {TotalPrice:C}",
            userId, dto.TotalItems, dto.TotalPrice);

        return Ok(dto);
    }

    /// <summary>
    /// Update quantity of an item in the cart.
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken)
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

        _logger.LogInformation("Updating item {ItemId} quantity to {Quantity} for user {UserId}",
            itemId, request.Quantity, userId);

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart is null)
        {
            _logger.LogWarning("No cart found for user {UserId}", userId);
            return NotFound(new { message = "Cart not found" });
        }

        var cartItem = cart.Items.FirstOrDefault(ci => ci.ItemId == itemId);
        if (cartItem is null)
        {
            _logger.LogWarning("Item {ItemId} not found in cart for user {UserId}", itemId, userId);
            return NotFound(new { message = $"Item {itemId} not found in cart" });
        }

        if (request.Quantity <= 0)
        {
            cart.Items.Remove(cartItem);
            _context.CartItems.Remove(cartItem);
            _logger.LogInformation("Removed item {ItemId} from cart (quantity was set to {Quantity})",
                itemId, request.Quantity);
        }
        else
        {
            var availableQuantity = await GetAvailableQuantityAsync(itemId, cancellationToken);
            if (request.Quantity > availableQuantity)
            {
                return Conflict(new { message = $"Only {availableQuantity} units are available in the warehouse." });
            }

            cartItem.Quantity = request.Quantity;
            _logger.LogInformation("Updated item {ItemId} quantity to {Quantity}", itemId, request.Quantity);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        EcommerceMetrics.CartMutations.Add(1, new TagList
        {
            { "operation", request.Quantity <= 0 ? "remove" : "update" }
        });

        var dto = MapToDto(cart);
        return Ok(dto);
    }

    /// <summary>
    /// Remove an item from the cart.
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveFromCart(Guid itemId)
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

        _logger.LogInformation("Removing item {ItemId} from cart for user {UserId}", itemId, userId);

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is null)
        {
            _logger.LogWarning("No cart found for user {UserId}", userId);
            return NotFound(new { message = "Cart not found" });
        }

        var cartItem = cart.Items.FirstOrDefault(ci => ci.ItemId == itemId);
        if (cartItem is null)
        {
            _logger.LogWarning("Item {ItemId} not found in cart for user {UserId}", itemId, userId);
            return NotFound(new { message = $"Item {itemId} not found in cart" });
        }

        cart.Items.Remove(cartItem);
        _context.CartItems.Remove(cartItem);
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        EcommerceMetrics.CartMutations.Add(1, new TagList
        {
            { "operation", "remove" }
        });

        _logger.LogInformation("Removed item {ItemId} from cart for user {UserId}", itemId, userId);

        var dto = MapToDto(cart);
        return Ok(dto);
    }

    /// <summary>
    /// Clear the entire cart.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> ClearCart()
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

        _logger.LogInformation("Clearing cart for user {UserId}", userId);

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is not null)
        {
            _context.CartItems.RemoveRange(cart.Items);
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync();
            EcommerceMetrics.CartMutations.Add(1, new TagList
            {
                { "operation", "clear" }
            });
            _logger.LogInformation("Cart cleared for user {UserId}", userId);
        }
        else
        {
            _logger.LogInformation("No cart to clear for user {UserId}", userId);
        }

        return NoContent();
    }

    private static CartDto MapToDto(Cart cart)
    {
        return new CartDto(
            cart.Id,
            cart.UserId,
            cart.Items.Select(ci => new CartItemDto(
                ci.Id,
                ci.ItemId,
                ci.ItemName,
                ci.UnitPrice,
                ci.Quantity,
                ci.UnitPrice * ci.Quantity
            )).ToList(),
            cart.TotalPrice,
            cart.TotalItems,
            cart.CreatedAt,
            cart.UpdatedAt
        );
    }

    private async Task<int> GetAvailableQuantityAsync(Guid itemId, CancellationToken cancellationToken) =>
        await _context.WarehouseInventories
            .AsNoTracking()
            .Where(inventory => inventory.WarehouseName == _inventoryService.WarehouseName && inventory.ItemId == itemId)
            .Select(inventory => inventory.QuantityOnHand - inventory.QuantityReserved)
            .SingleOrDefaultAsync(cancellationToken);
}
