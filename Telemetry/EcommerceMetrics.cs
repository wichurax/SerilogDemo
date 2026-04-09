using System.Diagnostics.Metrics;

namespace SerilogDemo.Telemetry;

public static class EcommerceMetrics
{
    public const string MeterName = "SerilogDemo.Ecommerce";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> CatalogRequests = Meter.CreateCounter<long>(
        name: "ecommerce.catalog.requests",
        unit: "requests",
        description: "Counts catalog API requests.");

    public static readonly Counter<long> BasketMutations = Meter.CreateCounter<long>(
        name: "ecommerce.basket.mutations",
        unit: "operations",
        description: "Counts basket changes grouped by operation.");

    public static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>(
        name: "ecommerce.orders.placed",
        unit: "orders",
        description: "Counts successfully placed orders.");

    public static readonly Histogram<double> OrderTotals = Meter.CreateHistogram<double>(
        name: "ecommerce.orders.total",
        unit: "currency",
        description: "Records total order value.");
}