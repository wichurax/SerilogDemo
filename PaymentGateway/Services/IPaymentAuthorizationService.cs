using PaymentGateway.Contracts;

namespace PaymentGateway.Services;

/// <summary>
/// Authorizes incoming payment requests and persists idempotent payment attempts.
/// </summary>
public interface IPaymentAuthorizationService
{
    /// <summary>
    /// Processes a payment authorization request.
    /// </summary>
    /// <param name="request">The payment authorization request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The authorization result for the payment attempt.</returns>
    Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken);
}