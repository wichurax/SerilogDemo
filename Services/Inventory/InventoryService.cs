using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using SerilogDemo.Data;
using SerilogDemo.Models;
using SerilogDemo.Options;
using SerilogDemo.Telemetry;

namespace SerilogDemo.Services.Inventory;

/// <inheritdoc cref="IInventoryService" />
public sealed class InventoryService : IInventoryService
{
    private readonly EcommerceDbContext _context;
    private readonly WarehouseOptions _warehouseOptions;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(EcommerceDbContext context, IOptions<WarehouseOptions> warehouseOptions, ILogger<InventoryService> logger)
    {
        _context = context;
        _warehouseOptions = warehouseOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string WarehouseName => _warehouseOptions.DefaultWarehouseName;

    /// <inheritdoc />
    public async Task<InventoryReservationResult> TryReserveAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken)
    {
        using var activity = EcommerceDiagnostics.ActivitySource.StartActivity("inventory.reserve", ActivityKind.Internal);
        activity?.SetTag("warehouse.name", WarehouseName);
        activity?.SetTag("inventory.line_count", lines.Count);

        foreach (var line in NormalizeLines(lines))
        {
            var updatedRowsCount = await _context.WarehouseInventories
                .Where(inventory => inventory.WarehouseName == WarehouseName
                    && inventory.ItemId == line.ItemId
                    && inventory.QuantityOnHand - inventory.QuantityReserved >= line.Quantity)
                .ExecuteUpdateAsync(BuildReservationUpdate(line.Quantity), cancellationToken);

            if (updatedRowsCount == 1)
            {
                continue;
            }

            var snapshot = await _context.WarehouseInventories
                .AsNoTracking()
                .Where(inventory => inventory.WarehouseName == WarehouseName && inventory.ItemId == line.ItemId)
                .Select(inventory => new
                {
                    inventory.QuantityOnHand,
                    inventory.QuantityReserved
                })
                .SingleOrDefaultAsync(cancellationToken);

            var availableQuantity = snapshot is null
                ? 0
                : snapshot.QuantityOnHand - snapshot.QuantityReserved;

            var message = snapshot is null
                ? $"Item '{line.ItemName}' is not stocked in warehouse {WarehouseName}."
                : $"Item '{line.ItemName}' is out of stock. Requested {line.Quantity}, available {availableQuantity}.";

            activity?.SetTag("inventory.outcome", "insufficient_stock");
            activity?.SetTag("inventory.failed_item_id", line.ItemId);
            EcommerceMetrics.InventoryReservations.Add(1, new TagList
            {
                { "operation", "reserve" },
                { "outcome", "failed" }
            });

            return new InventoryReservationResult(false, message, line.ItemId, availableQuantity);
        }

        activity?.SetTag("inventory.outcome", "reserved");
        EcommerceMetrics.InventoryReservations.Add(1, new TagList
        {
            { "operation", "reserve" },
            { "outcome", "success" }
        });

        return new InventoryReservationResult(true, "Inventory reserved.");
    }

    /// <inheritdoc />
    public async Task<InventoryMutationResult> ReleaseReservationAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken)
    {
        using var activity = EcommerceDiagnostics.ActivitySource.StartActivity("inventory.release", ActivityKind.Internal);
        activity?.SetTag("warehouse.name", WarehouseName);
        activity?.SetTag("inventory.line_count", lines.Count);

        foreach (var line in NormalizeLines(lines))
        {
            var updatedRowsCount = await _context.WarehouseInventories
                .Where(inventory => inventory.WarehouseName == WarehouseName
                    && inventory.ItemId == line.ItemId
                    && inventory.QuantityReserved >= line.Quantity)
                .ExecuteUpdateAsync(BuildReleaseUpdate(line.Quantity), cancellationToken);

            if (updatedRowsCount == 1)
            {
                continue;
            }

            activity?.SetTag("inventory.outcome", "release_failed");
            EcommerceMetrics.InventoryReservations.Add(1, new TagList
            {
                { "operation", "release" },
                { "outcome", "failed" }
            });

            return new InventoryMutationResult(false, $"Failed to release reserved stock for item '{line.ItemName}'.");
        }

        activity?.SetTag("inventory.outcome", "released");
        EcommerceMetrics.InventoryReservations.Add(1, new TagList
        {
            { "operation", "release" },
            { "outcome", "success" }
        });

        return new InventoryMutationResult(true, "Reserved stock released.");
    }

    /// <inheritdoc />
    public async Task<InventoryMutationResult> DeductReservedStockAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken)
    {
        using var activity = EcommerceDiagnostics.ActivitySource.StartActivity("inventory.deduct_reserved", ActivityKind.Internal);
        activity?.SetTag("warehouse.name", WarehouseName);
        activity?.SetTag("inventory.line_count", lines.Count);

        foreach (var line in NormalizeLines(lines))
        {
            var updatedRowsCount = await _context.WarehouseInventories
                .Where(inventory => inventory.WarehouseName == WarehouseName
                    && inventory.ItemId == line.ItemId
                    && inventory.QuantityReserved >= line.Quantity
                    && inventory.QuantityOnHand >= line.Quantity)
                .ExecuteUpdateAsync(BuildDeductionUpdate(line.Quantity), cancellationToken);

            if (updatedRowsCount == 1)
            {
                continue;
            }

            activity?.SetTag("inventory.outcome", "deduct_failed");
            EcommerceMetrics.InventoryReservations.Add(1, new TagList
            {
                { "operation", "deduct" },
                { "outcome", "failed" }
            });

            return new InventoryMutationResult(false, $"Failed to deduct reserved stock for item '{line.ItemName}'.");
        }

        activity?.SetTag("inventory.outcome", "deducted");
        EcommerceMetrics.InventoryReservations.Add(1, new TagList
        {
            { "operation", "deduct" },
            { "outcome", "success" }
        });

        return new InventoryMutationResult(true, "Reserved stock deducted from warehouse.");
    }

    /// <inheritdoc />
    public Task<InventoryAdjustmentResult> RestockAsync(Guid itemId, int quantity, string? reason, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return Task.FromResult(new InventoryAdjustmentResult(false, "Restock quantity must be greater than zero."));
        }

        return ApplyStockDeltaAsync(itemId, quantity, reason ?? "Restocked inventory.", "restock", cancellationToken);
    }

    /// <inheritdoc />
    public Task<InventoryAdjustmentResult> WriteOffAsync(Guid itemId, int quantity, string? reason, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return Task.FromResult(new InventoryAdjustmentResult(false, "Write-off quantity must be greater than zero."));
        }

        return ApplyStockDeltaAsync(itemId, -quantity, reason ?? "Inventory write-off.", "write_off", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryAdjustmentResult> RecountAsync(Guid itemId, int quantityOnHand, string? reason, CancellationToken cancellationToken)
    {
        if (quantityOnHand < 0)
        {
            return new InventoryAdjustmentResult(false, "Recount quantity cannot be negative.");
        }

        var snapshot = await _context.WarehouseInventories
            .AsNoTracking()
            .Where(inventory => inventory.WarehouseName == WarehouseName && inventory.ItemId == itemId)
            .Select(inventory => new
            {
                inventory.QuantityOnHand,
                inventory.QuantityReserved
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return new InventoryAdjustmentResult(false, $"Item {itemId} is not stocked in warehouse {WarehouseName}.");
        }

        if (quantityOnHand == snapshot.QuantityOnHand)
        {
            return new InventoryAdjustmentResult(
                true,
                "Inventory recount matched the current on-hand quantity.",
                snapshot.QuantityOnHand,
                snapshot.QuantityReserved,
                snapshot.QuantityOnHand - snapshot.QuantityReserved);
        }

        return await ApplyStockDeltaAsync(itemId, quantityOnHand - snapshot.QuantityOnHand, reason ?? "Inventory recount correction.", "recount", cancellationToken);
    }

    private async Task<InventoryAdjustmentResult> ApplyStockDeltaAsync(Guid itemId, int deltaQuantity, string? reason, string operation, CancellationToken cancellationToken)
    {
        using var activity = EcommerceDiagnostics.ActivitySource.StartActivity("inventory.adjust_stock", ActivityKind.Internal);
        activity?.SetTag("warehouse.name", WarehouseName);
        activity?.SetTag("item.id", itemId);
        activity?.SetTag("inventory.delta", deltaQuantity);
        activity?.SetTag("inventory.reason", reason);
        activity?.SetTag("inventory.operation", operation);

        if (deltaQuantity == 0)
        {
            return new InventoryAdjustmentResult(false, "Delta quantity cannot be 0.");
        }

        var updated = await _context.WarehouseInventories
            .Where(inventory => inventory.WarehouseName == WarehouseName
                && inventory.ItemId == itemId
                && inventory.QuantityOnHand + deltaQuantity >= 0
                && inventory.QuantityOnHand + deltaQuantity >= inventory.QuantityReserved)
            .ExecuteUpdateAsync(BuildDeltaUpdate(deltaQuantity), cancellationToken);

        if (updated != 1)
        {
            var snapshot = await _context.WarehouseInventories
                .AsNoTracking()
                .Where(inventory => inventory.WarehouseName == WarehouseName && inventory.ItemId == itemId)
                .Select(inventory => new
                {
                    inventory.QuantityOnHand,
                    inventory.QuantityReserved
                })
                .SingleOrDefaultAsync(cancellationToken);

            EcommerceMetrics.InventoryAdjustments.Add(1, new TagList
            {
                { "operation", operation },
                { "outcome", "failed" }
            });

            if (snapshot is null)
            {
                return new InventoryAdjustmentResult(false, $"Item {itemId} is not stocked in warehouse {WarehouseName}.");
            }

            return new InventoryAdjustmentResult(
                false,
                $"Stock adjustment would violate warehouse constraints. On hand {snapshot.QuantityOnHand}, reserved {snapshot.QuantityReserved}.",
                snapshot.QuantityOnHand,
                snapshot.QuantityReserved,
                snapshot.QuantityOnHand - snapshot.QuantityReserved);
        }

        var current = await _context.WarehouseInventories
            .AsNoTracking()
            .Where(inventory => inventory.WarehouseName == WarehouseName && inventory.ItemId == itemId)
            .Select(inventory => new
            {
                inventory.QuantityOnHand,
                inventory.QuantityReserved
            })
            .SingleAsync(cancellationToken);

        _logger.LogInformation(
            "Applied warehouse stock operation {Operation} for item {ItemId} by {DeltaQuantity} in warehouse {WarehouseName}. Reason: {Reason}",
            operation,
            itemId,
            deltaQuantity,
            WarehouseName,
            reason ?? "n/a");

        activity?.SetTag("inventory.outcome", "adjusted");
        EcommerceMetrics.InventoryAdjustments.Add(1, new TagList
        {
            { "operation", operation },
            { "outcome", "success" }
        });

        return new InventoryAdjustmentResult(
            true,
            "Warehouse inventory updated.",
            current.QuantityOnHand,
            current.QuantityReserved,
            current.QuantityOnHand - current.QuantityReserved);
    }

    private static IEnumerable<InventoryQuantityChange> NormalizeLines(IEnumerable<InventoryQuantityChange> lines) =>
        lines
            .Where(line => line.Quantity > 0)
            .GroupBy(line => new { line.ItemId, line.ItemName })
            .Select(group => new InventoryQuantityChange(group.Key.ItemId, group.Key.ItemName, group.Sum(line => line.Quantity)))
            .OrderBy(line => line.ItemId)
            .ToArray();

    private static Expression<Func<SetPropertyCalls<WarehouseInventory>, SetPropertyCalls<WarehouseInventory>>> BuildReservationUpdate(int reservationQuantity) =>
        update => update
            .SetProperty(inventory => inventory.QuantityReserved, inventory => inventory.QuantityReserved + reservationQuantity)
            .SetProperty(inventory => inventory.UpdatedAtUtc, _ => DateTime.UtcNow);

    private static Expression<Func<SetPropertyCalls<WarehouseInventory>, SetPropertyCalls<WarehouseInventory>>> BuildReleaseUpdate(int releaseQuantity) =>
        update => update
            .SetProperty(inventory => inventory.QuantityReserved, inventory => inventory.QuantityReserved - releaseQuantity)
            .SetProperty(inventory => inventory.UpdatedAtUtc, _ => DateTime.UtcNow);

    private static Expression<Func<SetPropertyCalls<WarehouseInventory>, SetPropertyCalls<WarehouseInventory>>> BuildDeductionUpdate(int deductionQuantity) =>
        update => update
            .SetProperty(inventory => inventory.QuantityReserved, inventory => inventory.QuantityReserved - deductionQuantity)
            .SetProperty(inventory => inventory.QuantityOnHand, inventory => inventory.QuantityOnHand - deductionQuantity)
            .SetProperty(inventory => inventory.UpdatedAtUtc, _ => DateTime.UtcNow);

    private static Expression<Func<SetPropertyCalls<WarehouseInventory>, SetPropertyCalls<WarehouseInventory>>> BuildDeltaUpdate(int deltaQuantity) =>
        update => update
            .SetProperty(inventory => inventory.QuantityOnHand, inventory => inventory.QuantityOnHand + deltaQuantity)
            .SetProperty(inventory => inventory.UpdatedAtUtc, _ => DateTime.UtcNow);
}