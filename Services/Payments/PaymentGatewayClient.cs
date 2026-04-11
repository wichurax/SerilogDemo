using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PaymentGateway.Contracts;
using SerilogDemo.Options;

namespace SerilogDemo.Services.Payments;

/// <inheritdoc cref="IPaymentGatewayClient" />
public sealed class PaymentGatewayClient : IPaymentGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentGatewayClient> _logger;

    public PaymentGatewayClient(HttpClient httpClient, IOptions<PaymentGatewayOptions> options, ILogger<PaymentGatewayClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TimeoutSeconds));
    }

    /// <inheritdoc />
    public async Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/payments/authorize", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AuthorizePaymentResponse>(cancellationToken);
            return payload ?? new AuthorizePaymentResponse(
                PaymentAttemptId: null,
                Status: PaymentAuthorizationStatus.TimedOut,
                ProviderCode: "empty_response",
                Message: "Payment gateway returned an empty response.",
                ProcessedAtUtc: DateTimeOffset.UtcNow,
                ProviderReference: null,
                IsIdempotentReplay: false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Payment gateway request timed out for order {OrderReference}", request.OrderReference);

            return new AuthorizePaymentResponse(
                PaymentAttemptId: null,
                Status: PaymentAuthorizationStatus.TimedOut,
                ProviderCode: "gateway_timeout",
                Message: "Payment gateway request timed out.",
                ProcessedAtUtc: DateTimeOffset.UtcNow,
                ProviderReference: null,
                IsIdempotentReplay: false);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Payment gateway call failed for order {OrderReference}", request.OrderReference);

            return new AuthorizePaymentResponse(
                PaymentAttemptId: null,
                Status: PaymentAuthorizationStatus.TimedOut,
                ProviderCode: "gateway_unavailable",
                Message: "Payment gateway is unavailable.",
                ProcessedAtUtc: DateTimeOffset.UtcNow,
                ProviderReference: null,
                IsIdempotentReplay: false);
        }
    }
}