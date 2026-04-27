using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentGateway.Contracts;
using PaymentGateway.Data;
using PaymentGateway.Models;
using PaymentGateway.Options;
using PaymentGateway.Telemetry;

namespace PaymentGateway.Services;

/// <inheritdoc cref="IPaymentAuthorizationService" />
public sealed class PaymentAuthorizationService : IPaymentAuthorizationService
{
    private readonly PaymentGatewayDbContext _dbContext;
    private readonly PaymentGatewaySimulationOptions _options;
    private readonly ILogger<PaymentAuthorizationService> _logger;

    public PaymentAuthorizationService(
        PaymentGatewayDbContext dbContext,
        IOptions<PaymentGatewaySimulationOptions> options,
        ILogger<PaymentAuthorizationService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken)
    {
        var scenario = request.Scenario ?? PaymentScenario.Success;

        using var activity = PaymentGatewayDiagnostics.ActivitySource.StartActivity("payment.authorize", System.Diagnostics.ActivityKind.Internal);
        activity?.SetTag("order.reference", request.OrderReference);
        activity?.SetTag("payment.method", request.PaymentMethodCode);
        activity?.SetTag("payment.scenario", scenario.ToString());

        var existingAttempt = await _dbContext.PaymentAttempts
            .SingleOrDefaultAsync(attempt => attempt.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingAttempt is not null)
        {
            _logger.LogInformation("Returning existing payment attempt {PaymentAttemptId} for idempotency key {IdempotencyKey}", existingAttempt.Id, request.IdempotencyKey);
            return Map(existingAttempt, isIdempotentReplay: true);
        }

        if (scenario == PaymentScenario.SlowSuccess)
        {
            await Task.Delay(_options.SlowSuccessDelayMilliseconds, cancellationToken);
        }
        else if (scenario == PaymentScenario.Timeout)
        {
            await Task.Delay(_options.TimeoutDelayMilliseconds, cancellationToken);
        }

        var (status, providerCode, message) = scenario switch
        {
            PaymentScenario.Decline => (PaymentAuthorizationStatus.Declined, "card_declined", "Payment was declined by the simulated provider."),
            PaymentScenario.Timeout => (PaymentAuthorizationStatus.TimedOut, "provider_timeout", "Provider did not confirm the payment in time."),
            _ => (PaymentAuthorizationStatus.Authorized, "authorized", "Payment authorized successfully.")
        };

        var paymentAttempt = new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = request.IdempotencyKey,
            OrderReference = request.OrderReference,
            UserId = request.UserId,
            PaymentMethodCode = request.PaymentMethodCode,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = status,
            ProviderCode = providerCode,
            Message = message,
            ProviderReference = status == PaymentAuthorizationStatus.Authorized ? $"pay_{Guid.NewGuid():N}" : null,
            Scenario = scenario.ToString(),
            ProcessedAtUtc = DateTime.UtcNow
        };

        _dbContext.PaymentAttempts.Add(paymentAttempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment attempt {PaymentAttemptId} processed for order {OrderReference} with status {Status} and provider code {ProviderCode}",
            paymentAttempt.Id,
            paymentAttempt.OrderReference,
            paymentAttempt.Status,
            paymentAttempt.ProviderCode);

        return Map(paymentAttempt, isIdempotentReplay: false);
    }

    private static AuthorizePaymentResponse Map(PaymentAttempt attempt, bool isIdempotentReplay)
    {
        return new AuthorizePaymentResponse(
            PaymentAttemptId: attempt.Id,
            Status: attempt.Status,
            ProviderCode: attempt.ProviderCode,
            Message: attempt.Message,
            ProcessedAtUtc: DateTime.SpecifyKind(attempt.ProcessedAtUtc, DateTimeKind.Utc),
            ProviderReference: attempt.ProviderReference,
            IsIdempotentReplay: isIdempotentReplay);
    }
}