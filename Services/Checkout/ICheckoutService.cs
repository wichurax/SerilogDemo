using PaymentGateway.Contracts;
using SerilogDemo.DTOs;

namespace SerilogDemo.Services.Checkout;

/// <summary>
/// Orchestrates checkout validation, payment authorization, order persistence, and inventory reservation.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Places an order for the given user and current cart contents.
    /// </summary>
    /// <param name="userId">The user placing the order.</param>
    /// <param name="request">The selected delivery and payment options.</param>
    /// <param name="paymentScenario">The optional simulated payment scenario.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The checkout outcome, including the persisted order when available.</returns>
    Task<CheckoutResult> PlaceOrderAsync(string userId, PlaceOrderRequest request, PaymentScenario? paymentScenario, CancellationToken cancellationToken);
}