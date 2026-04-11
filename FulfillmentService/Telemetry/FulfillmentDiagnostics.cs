using System.Diagnostics;

namespace FulfillmentService.Telemetry;

public static class FulfillmentDiagnostics
{
    public const string ActivitySourceName = "FulfillmentService.Consumer";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}