using System.Text.Json.Serialization;

namespace PaymentGateway.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<PaymentAuthorizationStatus>))]
public enum PaymentAuthorizationStatus
{
    Authorized,
    Declined,
    TimedOut
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentScenario>))]
public enum PaymentScenario
{
    Success,
    Decline,
    SlowSuccess,
    Timeout
}

public sealed record AuthorizePaymentRequest(
    string IdempotencyKey,
    string OrderReference,
    string UserId,
    string PaymentMethodCode,
    decimal Amount,
    string Currency,
    PaymentScenario? Scenario = null);

public sealed record AuthorizePaymentResponse(
    Guid? PaymentAttemptId,
    PaymentAuthorizationStatus Status,
    string ProviderCode,
    string Message,
    DateTimeOffset ProcessedAtUtc,
    string? ProviderReference,
    bool IsIdempotentReplay);