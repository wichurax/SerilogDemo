namespace FulfillmentService.Options;

public sealed class FulfillmentServiceOptions
{
    public const string SectionName = "Fulfillment";

    public string WarehouseName { get; set; } = "demo-warehouse-01";

    public int ProcessingDelayMilliseconds { get; set; } = 350;
}