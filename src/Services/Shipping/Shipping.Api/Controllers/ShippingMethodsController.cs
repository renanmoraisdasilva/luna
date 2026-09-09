using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Shipping.Api.Controllers;

[ApiController]
[Route("api/v1/shipping-methods")]
public sealed class ShippingMethodsController(GetShippingMethodsHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetShippingMethodsResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(cancellationToken));
}
