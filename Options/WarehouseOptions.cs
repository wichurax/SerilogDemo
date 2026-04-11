namespace SerilogDemo.Options;

public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouse";

    public string DefaultWarehouseName { get; set; } = "demo-warehouse-01";
}