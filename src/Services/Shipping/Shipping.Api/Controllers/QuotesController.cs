using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Contracts;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Shipping.Api.Controllers;

[ApiController]
[Route("api/v1/quotes")]
public sealed class QuotesController(QuoteShippingHandler handler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LunaServicePolicies.OrdersShippingShipmentsWrite)]
    public async Task<ActionResult<ShippingQuoteResponse>> Create(
        QuoteShippingRequest request,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(
            new QuoteShippingCommand(request.OrderId, request.ShippingMethodCode, request.Country, request.PostalCode),
            cancellationToken));
}
