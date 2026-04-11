namespace PaymentGateway.Options;

public sealed class PaymentGatewaySimulationOptions
{
    public const string SectionName = "PaymentGateway";

    public int SlowSuccessDelayMilliseconds { get; set; } = 1200;

    public int TimeoutDelayMilliseconds { get; set; } = 2500;
}