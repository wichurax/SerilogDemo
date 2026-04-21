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
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(EcommerceDbContext context, IInventoryService inventoryService, ILogger<ItemsController> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get all items in the catalog.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItems([FromQuery] string? category = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching items from catalog. Category filter: {Category}. Search filter: {Search}",
            category ?? "none",
            search ?? "none");

        var query = _context.Items.AsNoTracking().AsQueryable();
        var warehouseName = _inventoryService.WarehouseName;
        var normalizedCategory = category?.Trim();
        var normalizedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            query = query.Where(i => EF.Functions.ILike(i.Category, normalizedCategory));
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var searchPattern = $"%{normalizedSearch}%";
            query = query.Where(i =>
                EF.Functions.ILike(i.Name, searchPattern) ||
                EF.Functions.ILike(i.Description, searchPattern) ||
                EF.Functions.ILike(i.Category, searchPattern));
        }

        var items = await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.Name)
            .ProjectCatalogItems(_context.WarehouseInventories.AsNoTracking(), warehouseName)
            .ToListAsync(cancellationToken);

        EcommerceMetrics.CatalogRequests.Add(1, new TagList
        {
            { "operation", "list" },
            { "filtered", string.IsNullOrWhiteSpace(normalizedCategory) && string.IsNullOrWhiteSpace(normalizedSearch) ? "false" : "true" }
        });

        _logger.LogInformation("Returning {Count} items from catalog", items.Count);
        return Ok(items);
    }

    /// <summary>
    /// Get a specific item by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemDto>> GetItem(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching item {ItemId}", id);
        var warehouseName = _inventoryService.WarehouseName;

        var item = await _context.Items
            .AsNoTracking()
            .Where(i => i.Id == id)
            .ProjectCatalogItems(_context.WarehouseInventories.AsNoTracking(), warehouseName)
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            _logger.LogWarning("Item {ItemId} not found", id);
            return NotFound(new { message = $"Item with ID {id} not found" });
        }

        EcommerceMetrics.CatalogRequests.Add(1, new TagList
        {
            { "operation", "details" }
        });

        _logger.LogInformation("Returning item {ItemId}: {ItemName}", id, item.Name);
        return Ok(item);
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
    public async Task<ActionResult<IEnumerable<ItemDto>>> GetItemsByCategory(string category, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching items for category: {Category}", category);
        var warehouseName = _inventoryService.WarehouseName;
        var normalizedCategory = category.Trim();

        var items = await ProjectItems(
                _context.Items
                    .AsNoTracking()
                    .Where(i => EF.Functions.ILike(i.Category, normalizedCategory))
                    .OrderBy(i => i.Name),
                warehouseName)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            _logger.LogWarning("No items found for category: {Category}", category);
            return NotFound(new { message = $"No items found in category '{category}'" });
        }

        _logger.LogInformation("Returning {Count} items for category: {Category}", items.Count, category);
        return Ok(items);
    }

    private IQueryable<ItemDto> ProjectItems(IQueryable<Item> itemsQuery, string warehouseName) =>
        itemsQuery.ProjectCatalogItems(_context.WarehouseInventories.AsNoTracking(), warehouseName);
}
