using System.Diagnostics;

namespace SerilogDemo.Telemetry;

public static class EcommerceDiagnostics
{
    public const string ActivitySourceName = "SerilogDemo.Checkout";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}