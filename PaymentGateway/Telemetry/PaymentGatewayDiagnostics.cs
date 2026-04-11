using System.Diagnostics;

namespace PaymentGateway.Telemetry;

public static class PaymentGatewayDiagnostics
{
    public const string ActivitySourceName = "PaymentGateway.Authorization";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}