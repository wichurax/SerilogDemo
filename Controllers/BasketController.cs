using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Models;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasketController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<BasketController> _logger;

    public BasketController(EcommerceDbContext context, ILogger<BasketController> logger)
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
    /// Get current user's basket.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<BasketDto>> GetBasket()
    {
        string userId;
        try
        {
            userId = GetUserId();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Basket request without user ID");
            return BadRequest(new { message = ex.Message });
        }

        _logger.LogInformation("Fetching basket for user {UserId}", userId);

        var basket = await _context.Baskets
            .Include(b => b.Items)
            .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is null)
        {
            _logger.LogInformation("No basket found for user {UserId}, returning empty basket", userId);
            return Ok(new BasketDto(Guid.Empty, userId, [], 0, 0, DateTime.UtcNow, DateTime.UtcNow));
        }

        var dto = MapToDto(basket);
        _logger.LogInformation("Returning basket for user {UserId} with {ItemCount} items, total: {TotalPrice:C}", 
            userId, dto.TotalItems, dto.TotalPrice);
        return Ok(dto);
    }

    /// <summary>
    /// Add an item to the basket.
    /// </summary>
    [HttpPost("items")]
    public async Task<ActionResult<BasketDto>> AddToBasket([FromBody] AddToBasketRequest request)
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

        _logger.LogInformation("Adding item {ItemId} (quantity: {Quantity}) to basket for user {UserId}", 
            request.ItemId, request.Quantity, userId);

        var item = await _context.Items.FindAsync(request.ItemId);
        if (item is null)
        {
            _logger.LogWarning("Item {ItemId} not found when adding to basket", request.ItemId);
            return NotFound(new { message = $"Item with ID {request.ItemId} not found" });
        }

        var basket = await _context.Baskets
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is null)
        {
            _logger.LogInformation("Creating new basket for user {UserId}", userId);
            basket = new Basket
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };
            _context.Baskets.Add(basket);
        }

        var existingItem = basket.Items.FirstOrDefault(bi => bi.ItemId == request.ItemId);
        if (existingItem is not null)
        {
            existingItem.Quantity += request.Quantity;
            _logger.LogInformation("Updated quantity of item {ItemId} in basket to {Quantity}", 
                request.ItemId, existingItem.Quantity);
        }
        else
        {
            var basketItem = new BasketItem
            {
                Id = Guid.NewGuid(),
                BasketId = basket.Id,
                ItemId = item.Id,
                ItemName = item.Name,
                UnitPrice = item.Price,
                Quantity = request.Quantity
            };
            _context.BasketItems.Add(basketItem);
            _logger.LogInformation("Added new item {ItemId} ({ItemName}) to basket", item.Id, item.Name);
        }

        basket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        basket = await _context.Baskets
            .Include(b => b.Items)
            .ThenInclude(bi => bi.Item)
            .FirstAsync(b => b.Id == basket.Id);

        var dto = MapToDto(basket);
        _logger.LogInformation("Basket updated for user {UserId}. Total items: {TotalItems}, Total: {TotalPrice:C}", 
            userId, dto.TotalItems, dto.TotalPrice);
        
        return Ok(dto);
    }

    /// <summary>
    /// Update quantity of an item in the basket.
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<BasketDto>> UpdateBasketItem(Guid itemId, [FromBody] UpdateBasketItemRequest request)
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

        var basket = await _context.Baskets
            .Include(b => b.Items)
            .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is null)
        {
            _logger.LogWarning("No basket found for user {UserId}", userId);
            return NotFound(new { message = "Basket not found" });
        }

        var basketItem = basket.Items.FirstOrDefault(bi => bi.ItemId == itemId);
        if (basketItem is null)
        {
            _logger.LogWarning("Item {ItemId} not found in basket for user {UserId}", itemId, userId);
            return NotFound(new { message = $"Item {itemId} not found in basket" });
        }

        if (request.Quantity <= 0)
        {
            basket.Items.Remove(basketItem);
            _context.BasketItems.Remove(basketItem);
            _logger.LogInformation("Removed item {ItemId} from basket (quantity was set to {Quantity})", 
                itemId, request.Quantity);
        }
        else
        {
            basketItem.Quantity = request.Quantity;
            _logger.LogInformation("Updated item {ItemId} quantity to {Quantity}", itemId, request.Quantity);
        }

        basket.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(basket);
        return Ok(dto);
    }

    /// <summary>
    /// Remove an item from the basket.
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<BasketDto>> RemoveFromBasket(Guid itemId)
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

        _logger.LogInformation("Removing item {ItemId} from basket for user {UserId}", itemId, userId);

        var basket = await _context.Baskets
            .Include(b => b.Items)
            .ThenInclude(bi => bi.Item)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is null)
        {
            _logger.LogWarning("No basket found for user {UserId}", userId);
            return NotFound(new { message = "Basket not found" });
        }

        var basketItem = basket.Items.FirstOrDefault(bi => bi.ItemId == itemId);
        if (basketItem is null)
        {
            _logger.LogWarning("Item {ItemId} not found in basket for user {UserId}", itemId, userId);
            return NotFound(new { message = $"Item {itemId} not found in basket" });
        }

        basket.Items.Remove(basketItem);
        _context.BasketItems.Remove(basketItem);
        basket.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed item {ItemId} from basket for user {UserId}", itemId, userId);
        
        var dto = MapToDto(basket);
        return Ok(dto);
    }

    /// <summary>
    /// Clear the entire basket.
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> ClearBasket()
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

        _logger.LogInformation("Clearing basket for user {UserId}", userId);

        var basket = await _context.Baskets
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.UserId == userId);

        if (basket is not null)
        {
            _context.BasketItems.RemoveRange(basket.Items);
            _context.Baskets.Remove(basket);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Basket cleared for user {UserId}", userId);
        }
        else
        {
            _logger.LogInformation("No basket to clear for user {UserId}", userId);
        }

        return NoContent();
    }

    private static BasketDto MapToDto(Basket basket)
    {
        return new BasketDto(
            basket.Id,
            basket.UserId,
            basket.Items.Select(bi => new BasketItemDto(
                bi.Id,
                bi.ItemId,
                bi.ItemName,
                bi.UnitPrice,
                bi.Quantity,
                bi.UnitPrice * bi.Quantity
            )).ToList(),
            basket.TotalPrice,
            basket.TotalItems,
            basket.CreatedAt,
            basket.UpdatedAt
        );
    }
}
