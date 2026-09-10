using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Contracts;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Shipping.Api.Controllers;

[ApiController]
[Route("api/v1/shipments")]
public sealed class ShipmentsController(CreateShipmentHandler handler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LunaServicePolicies.OrdersShippingShipmentsWrite)]
    public async Task<ActionResult<ShipmentResponse>> Create(
        CreateShipmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(
            new CreateShipmentCommand(request.OrderId, request.QuoteId),
            cancellationToken));
}
