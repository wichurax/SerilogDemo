using PaymentGateway.Contracts;

namespace PaymentGateway.Models;

public class PaymentAttempt
{
    public Guid Id { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string OrderReference { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string PaymentMethodCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public PaymentAuthorizationStatus Status { get; set; }

    public string ProviderCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ProviderReference { get; set; }

    public string? Scenario { get; set; }

    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}