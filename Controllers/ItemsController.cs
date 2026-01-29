using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(EcommerceDbContext context, ILogger<ItemsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all items in the catalog.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItems([FromQuery] string? category = null)
    {
        _logger.LogInformation("Fetching items from catalog. Category filter: {Category}", category ?? "none");

        var query = _context.Items.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category.ToLower() == category.ToLower());
        }

        var items = await query
            .Select(i => new ItemDto(i.Id, i.Name, i.Description, i.Price, i.Category, i.ImageUrl))
            .ToListAsync();

        _logger.LogInformation("Returning {Count} items from catalog", items.Count);
        return Ok(items);
    }

    /// <summary>
    /// Get a specific item by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> GetItem(Guid id)
    {
        _logger.LogInformation("Fetching item {ItemId}", id);

        var item = await _context.Items.FindAsync(id);

        if (item is null)
        {
            _logger.LogWarning("Item {ItemId} not found", id);
            return NotFound(new { message = $"Item with ID {id} not found" });
        }

        _logger.LogInformation("Returning item {ItemId}: {ItemName}", id, item.Name);
        return Ok(new ItemDto(item.Id, item.Name, item.Description, item.Price, item.Category, item.ImageUrl));
    }

    /// <summary>
    /// Get available categories.
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        _logger.LogInformation("Fetching item categories");

        var categories = await _context.Items
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        _logger.LogInformation("Returning {Count} categories", categories.Count);
        return Ok(categories);
    }

    /// <summary>
    /// Get items by category.
    /// </summary>
    [HttpGet("categories/{category}")]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItemsByCategory(string category)
    {
        _logger.LogInformation("Fetching items for category: {Category}", category);

        var items = await _context.Items
            .Where(i => i.Category.ToLower() == category.ToLower())
            .Select(i => new ItemDto(i.Id, i.Name, i.Description, i.Price, i.Category, i.ImageUrl))
            .ToListAsync();

        if (items.Count == 0)
        {
            _logger.LogWarning("No items found for category: {Category}", category);
            return NotFound(new { message = $"No items found in category '{category}'" });
        }

        _logger.LogInformation("Returning {Count} items for category: {Category}", items.Count, category);
        return Ok(items);
    }
}
