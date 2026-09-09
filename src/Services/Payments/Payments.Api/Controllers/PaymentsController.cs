using Luna.Payments.Application.Authorization;
using Luna.Payments.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Payments.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(AuthorizePaymentHandler authorizePayment) : ControllerBase
{
    [HttpPost("authorize")]
    public async Task<ActionResult<AuthorizePaymentResponse>> Authorize(
        AuthorizePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authorizePayment.HandleAsync(
            new AuthorizePaymentCommand(
                request.OrderId,
                request.Amount,
                request.Currency,
                request.PaymentMethod),
            cancellationToken);
        return Ok(response);
    }
}
