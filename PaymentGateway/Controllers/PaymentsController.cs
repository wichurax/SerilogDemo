using Microsoft.AspNetCore.Mvc;
using PaymentGateway.Contracts;
using PaymentGateway.Services;

namespace PaymentGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentAuthorizationService _paymentAuthorizationService;

    public PaymentsController(IPaymentAuthorizationService paymentAuthorizationService)
    {
        _paymentAuthorizationService = paymentAuthorizationService;
    }

    [HttpPost("authorize")]
    public async Task<ActionResult<AuthorizePaymentResponse>> Authorize([FromBody] AuthorizePaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || string.IsNullOrWhiteSpace(request.OrderReference))
        {
            return BadRequest(new { message = "Idempotency key and order reference are required." });
        }

        var result = await _paymentAuthorizationService.AuthorizeAsync(request, cancellationToken);
        return Ok(result);
    }
}