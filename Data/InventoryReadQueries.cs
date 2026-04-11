using Microsoft.EntityFrameworkCore;
using SerilogDemo.DTOs;
using SerilogDemo.Models;

namespace SerilogDemo.Data;

public static class InventoryReadQueries
{
    public static IQueryable<ItemDto> ProjectCatalogItems(
        this IQueryable<Item> itemsQuery,
        IQueryable<WarehouseInventory> warehouseInventories,
        string warehouseName)
    {
        var inventoryQuery = warehouseInventories.Where(inventory => inventory.WarehouseName == warehouseName);

        // TODO - prefer EF Core extension methods over SQL-like queries
        return from item in itemsQuery
               join inventory in inventoryQuery on item.Id equals inventory.ItemId into inventoryGroup
               from inventory in inventoryGroup.DefaultIfEmpty()
               select new ItemDto(
                   item.Id,
                   item.Name,
                   item.Description,
                   item.Price,
                   item.Category,
                   item.ImageUrl,
                   inventory == null ? 0 : inventory.QuantityOnHand,
                   inventory == null ? 0 : inventory.QuantityReserved,
                   inventory == null ? 0 : inventory.QuantityOnHand - inventory.QuantityReserved);
    }

    public static IQueryable<WarehouseInventoryDto> ProjectWarehouseItems(this IQueryable<WarehouseInventory> inventoryQuery) => 
        inventoryQuery.Select(inventory => new WarehouseInventoryDto(
            inventory.ItemId,
            inventory.Item.Name,
            inventory.Item.Category,
            inventory.WarehouseName,
            inventory.QuantityOnHand,
            inventory.QuantityReserved,
            inventory.QuantityOnHand - inventory.QuantityReserved,
            inventory.UpdatedAtUtc));
}