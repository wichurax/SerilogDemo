using System.Diagnostics;

namespace FulfillmentApi.Telemetry;

public static class FulfillmentDiagnostics
{
    public const string ActivitySourceName = "FulfillmentApi.Consumer";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}