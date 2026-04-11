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

    public static readonly Counter<long> CheckoutAttempts = Meter.CreateCounter<long>(
        name: "ecommerce.checkout.attempts",
        unit: "attempts",
        description: "Counts checkout attempts grouped by outcome.");

    public static readonly Counter<long> PaymentAuthorizations = Meter.CreateCounter<long>(
        name: "ecommerce.payment.authorizations",
        unit: "attempts",
        description: "Counts payment authorization results grouped by payment method and outcome.");

    public static readonly Histogram<double> OrderTotals = Meter.CreateHistogram<double>(
        name: "ecommerce.orders.total",
        unit: "currency",
        description: "Records total order value.");

    public static readonly Histogram<double> CheckoutDuration = Meter.CreateHistogram<double>(
        name: "ecommerce.checkout.duration",
        unit: "ms",
        description: "Records end-to-end checkout duration.");

    public static readonly Counter<long> InventoryReservations = Meter.CreateCounter<long>(
        name: "ecommerce.inventory.reservations",
        unit: "operations",
        description: "Counts inventory reserve, release, and deduct operations grouped by outcome.");

    public static readonly Counter<long> InventoryAdjustments = Meter.CreateCounter<long>(
        name: "ecommerce.inventory.adjustments",
        unit: "operations",
        description: "Counts warehouse stock adjustments grouped by outcome.");

    public static readonly Counter<long> FulfillmentCallbacks = Meter.CreateCounter<long>(
        name: "ecommerce.fulfillment.callbacks",
        unit: "events",
        description: "Counts fulfillment progress events projected into orders.");
}