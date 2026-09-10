using Luna.Payments.Application.Authorization;
using Luna.Payments.Contracts;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Luna.Payments.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(AuthorizePaymentHandler authorizePayment) : ControllerBase
{
    [HttpPost("authorize")]
    [Authorize(Policy = LunaServicePolicies.OrdersPaymentsAuthorize)]
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
