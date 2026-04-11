namespace SerilogDemo.Options;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string BaseUrl { get; set; } = "http://localhost:5194";

    public int TimeoutSeconds { get; set; } = 5;
}