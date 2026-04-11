using Microsoft.EntityFrameworkCore;
using SerilogDemo.Data;
using Xunit;

namespace SerilogDemo.IntegrationTests;

public class QueryShapeTests
{
    private const string ConnectionString = "Host=localhost;Database=serilogdemo_query_tests;Username=test;Password=test";
    private const string WarehouseName = "demo-warehouse-01";

    [Fact]
    public void Catalog_projection_uses_single_left_join_and_ilike_filter()
    {
        using var dbContext = CreateDbContext();

        var sql = dbContext.Items
            .AsNoTracking()
            .Where(item => EF.Functions.ILike(item.Category, "Electronics"))
            .OrderBy(item => item.Name)
            .ProjectCatalogItems(dbContext.WarehouseInventories.AsNoTracking(), WarehouseName)
            .ToQueryString();

        Assert.Contains("LEFT JOIN", sql.ToUpperInvariant());
        Assert.Contains("\"WarehouseInventories\"", sql);
        Assert.Contains("ILIKE", sql.ToUpperInvariant());
        Assert.DoesNotContain("lower(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warehouse_projection_joins_items_once_without_redundant_subqueries()
    {
        using var dbContext = CreateDbContext();

        var sql = dbContext.WarehouseInventories
            .AsNoTracking()
            .Where(inventory => inventory.WarehouseName == WarehouseName)
            .OrderBy(inventory => inventory.Item.Category)
            .ThenBy(inventory => inventory.Item.Name)
            .ProjectWarehouseItems()
            .ToQueryString();

        Assert.Contains("JOIN \"ITEMS\"", sql.ToUpperInvariant());
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lower(", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static EcommerceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EcommerceDbContext(options);
    }
}