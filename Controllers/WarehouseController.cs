using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using SerilogDemo.DTOs;
using SerilogDemo.Services.Inventory;

namespace SerilogDemo.Controllers;

[ApiController]
[Route("api/warehouse")]
public class WarehouseController : ControllerBase
{
    private readonly EcommerceDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(EcommerceDbContext context, IInventoryService inventoryService, ILogger<WarehouseController> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<WarehouseInventoryDto>>> GetWarehouseItems(CancellationToken cancellationToken)
    {
        var warehouseName = _inventoryService.WarehouseName;

        var items = await _context.WarehouseInventories
            .AsNoTracking()
            .Where(inventory => inventory.WarehouseName == warehouseName)
            .OrderBy(inventory => inventory.Item.Category)
            .ThenBy(inventory => inventory.Item.Name)
            .ProjectWarehouseItems()
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("items/{itemId:guid}")]
    public async Task<ActionResult<WarehouseInventoryDto>> GetWarehouseItem(Guid itemId, CancellationToken cancellationToken)
    {
        var warehouseName = _inventoryService.WarehouseName;
        var inventory = await _context.WarehouseInventories
            .AsNoTracking()
            .Where(item => item.WarehouseName == warehouseName && item.ItemId == itemId)
            .ProjectWarehouseItems()
            .SingleOrDefaultAsync(cancellationToken);

        if (inventory is null)
        {
            return NotFound(new { message = $"Warehouse inventory for item {itemId} not found." });
        }

        return Ok(inventory);
    }

    [HttpPost("items/{itemId:guid}/restock")]
    public async Task<ActionResult<WarehouseInventoryCommandResultDto>> RestockItem(Guid itemId, [FromBody] RestockInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.RestockAsync(itemId, request.Quantity, request.Reason, cancellationToken);
        return CreateCommandResponse(itemId, "restock", result);
    }

    [HttpPost("items/{itemId:guid}/write-off")]
    public async Task<ActionResult<WarehouseInventoryCommandResultDto>> WriteOffItem(Guid itemId, [FromBody] WriteOffInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.WriteOffAsync(itemId, request.Quantity, request.Reason, cancellationToken);
        return CreateCommandResponse(itemId, "write-off", result);
    }

    [HttpPost("items/{itemId:guid}/recount")]
    public async Task<ActionResult<WarehouseInventoryCommandResultDto>> RecountItem(Guid itemId, [FromBody] RecountInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.RecountAsync(itemId, request.QuantityOnHand, request.Reason, cancellationToken);
        return CreateCommandResponse(itemId, "recount", result);
    }

    private ActionResult<WarehouseInventoryCommandResultDto> CreateCommandResponse(Guid itemId, string operation, InventoryAdjustmentResult result)
    {
        if (!result.Succeeded)
        {
            _logger.LogWarning("Warehouse {Operation} command rejected for item {ItemId}: {Message}", operation, itemId, result.Message);
            return Conflict(new { message = result.Message, result.QuantityOnHand, result.QuantityReserved, result.AvailableQuantity });
        }

        return Ok(new WarehouseInventoryCommandResultDto(
            result.Message,
            result.QuantityOnHand ?? 0,
            result.QuantityReserved ?? 0,
            result.AvailableQuantity ?? 0));
    }
}