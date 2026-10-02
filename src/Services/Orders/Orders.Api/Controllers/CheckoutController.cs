using Luna.Authentication;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Contracts.Checkout;
using Luna.Contracts.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Orders.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme)]
[Route("api/v1/orders/checkout")]
public sealed class CheckoutController(
    CheckoutHandler checkout,
    ILogger<CheckoutController> logger) : ControllerBase
{
    [HttpPost]
    public Task<CheckoutResponse> Submit(CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!User.TryGetSubjectId(out var customerId))
        {
            throw new UnauthenticatedCustomerException();
        }

        logger.LogInformation("Checkout API request accepted for customer {CustomerId}", customerId);
        return checkout.HandleAsync(new CheckoutCommand(customerId, request, idempotencyKey ?? string.Empty), cancellationToken);
    }
}
