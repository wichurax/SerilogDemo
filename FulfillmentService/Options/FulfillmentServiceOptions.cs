namespace FulfillmentService.Options;

public sealed class FulfillmentServiceOptions
{
    public const string SectionName = "Fulfillment";

    public string WarehouseName { get; set; } = "demo-warehouse-01";

    public int ConsumerPrefetchCount { get; set; } = 16;

    public int OutboxBatchSize { get; set; } = 50;
}