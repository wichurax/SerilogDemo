using PaymentGateway.Contracts;

namespace SerilogDemo.Services.Payments;

/// <summary>
/// Sends payment authorization requests to the external payment gateway service.
/// </summary>
public interface IPaymentGatewayClient
{
    /// <summary>
    /// Requests authorization for a payment attempt.
    /// </summary>
    /// <param name="request">The payment authorization request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The gateway response describing the authorization outcome.</returns>
    Task<AuthorizePaymentResponse> AuthorizeAsync(AuthorizePaymentRequest request, CancellationToken cancellationToken);
}