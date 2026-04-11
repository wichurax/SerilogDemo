using SerilogDemo.Models;

namespace SerilogDemo.Services.Inventory;

/// <summary>
/// Coordinates inventory reservations and operational stock movements for the configured warehouse.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Gets the warehouse name used for inventory reads and writes.
    /// </summary>
    string WarehouseName { get; }

    /// <summary>
    /// Attempts to reserve stock for the requested line items.
    /// </summary>
    /// <param name="lines">The requested inventory quantities grouped by catalog item.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The reservation outcome, including the first failed item when stock is insufficient.</returns>
    Task<InventoryReservationResult> TryReserveAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken);

    /// <summary>
    /// Releases previously reserved stock for the requested line items.
    /// </summary>
    /// <param name="lines">The reserved inventory quantities to release.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mutation outcome.</returns>
    Task<InventoryMutationResult> ReleaseReservationAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken);

    /// <summary>
    /// Converts reserved stock into shipped stock by deducting it from on-hand inventory.
    /// </summary>
    /// <param name="lines">The reserved inventory quantities to deduct.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mutation outcome.</returns>
    Task<InventoryMutationResult> DeductReservedStockAsync(IReadOnlyCollection<InventoryQuantityChange> lines, CancellationToken cancellationToken);

    /// <summary>
    /// Adds physical stock to the warehouse for the specified item.
    /// </summary>
    /// <param name="itemId">The catalog item identifier.</param>
    /// <param name="quantity">The number of units to add.</param>
    /// <param name="reason">The operational reason for the restock.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resulting on-hand and reserved quantities after the restock.</returns>
    Task<InventoryAdjustmentResult> RestockAsync(Guid itemId, int quantity, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Removes physical stock from the warehouse for loss, damage, or other write-off reasons.
    /// </summary>
    /// <param name="itemId">The catalog item identifier.</param>
    /// <param name="quantity">The number of units to remove.</param>
    /// <param name="reason">The operational reason for the write-off.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resulting on-hand and reserved quantities after the write-off.</returns>
    Task<InventoryAdjustmentResult> WriteOffAsync(Guid itemId, int quantity, string? reason, CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles the recorded on-hand quantity with a physical stock count.
    /// </summary>
    /// <param name="itemId">The catalog item identifier.</param>
    /// <param name="quantityOnHand">The counted on-hand quantity.</param>
    /// <param name="reason">The operational reason for the recount.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resulting on-hand and reserved quantities after the recount.</returns>
    Task<InventoryAdjustmentResult> RecountAsync(Guid itemId, int quantityOnHand, string? reason, CancellationToken cancellationToken);
}

public sealed record InventoryQuantityChange(Guid ItemId, string ItemName, int Quantity);

public sealed record InventoryReservationResult(bool Succeeded, string Message, Guid? FailedItemId = null, int? AvailableQuantity = null);

public sealed record InventoryMutationResult(bool Succeeded, string Message);

public sealed record InventoryAdjustmentResult(
    bool Succeeded,
    string Message,
    int? QuantityOnHand = null,
    int? QuantityReserved = null,
    int? AvailableQuantity = null);