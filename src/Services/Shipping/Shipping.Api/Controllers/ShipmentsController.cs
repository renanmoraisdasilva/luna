using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Api.Authorization;
using Luna.Authentication;
using Luna.Shipping.Contracts;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Shipping.Api.Controllers;

[ApiController]
[Route("api/v1/shipments")]
public sealed class ShipmentsController(
    CreateShipmentHandler createShipment,
    GetShipmentsHandler getShipments,
    GetShipmentHandler getShipment,
    GetShipmentTrackingHandler getShipmentTracking,
    MarkShipmentInTransitHandler markInTransit,
    MarkShipmentDeliveredHandler markDelivered) : ControllerBase
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme, Policy = ShippingAuthorizationPolicies.Operations)]
    public Task<ShipmentListResponse> GetList(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        getShipments.HandleAsync(new ShipmentQuery(status, search, page, pageSize), cancellationToken);

    [HttpGet("{shipmentId:guid}")]
    [Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme, Policy = ShippingAuthorizationPolicies.Operations)]
    public async Task<ActionResult<ShipmentDetailResponse>> GetById(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await getShipment.HandleAsync(shipmentId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{shipmentId:guid}/tracking")]
    [Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme, Policy = ShippingAuthorizationPolicies.Customer)]
    [ProducesResponseType<ShipmentTrackingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentTrackingResponse>> GetTracking(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetSubjectId(out var customerId))
        {
            throw new InvalidOperationException("The authenticated customer ID is missing or invalid.");
        }

        var response = await getShipmentTracking.HandleAsync(shipmentId, customerId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = LunaServicePolicies.OrdersShippingShipmentsWrite)]
    public async Task<ActionResult<ShipmentResponse>> Create(
        CreateShipmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await createShipment.HandleAsync(
            new CreateShipmentCommand(
                request.OrderId,
                request.QuoteId,
                new ShipmentRecipientCommand(
                    request.Recipient.CustomerId,
                    request.Recipient.FullName,
                    request.Recipient.AddressLine1,
                    request.Recipient.AddressLine2,
                    request.Recipient.City,
                    request.Recipient.StateOrProvince,
                    request.Recipient.PostalCode,
                    request.Recipient.Country)),
            cancellationToken));

    [HttpPost("{shipmentId:guid}/in-transit")]
    [Authorize(Policy = LunaServicePolicies.OrdersShippingShipmentsWrite)]
    public async Task<ActionResult<ShipmentResponse>> MarkInTransit(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await markInTransit.HandleAsync(new MarkShipmentInTransitCommand(shipmentId), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{shipmentId:guid}/delivered")]
    [Authorize(Policy = LunaServicePolicies.OrdersShippingShipmentsWrite)]
    public async Task<ActionResult<ShipmentResponse>> MarkDelivered(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await markDelivered.HandleAsync(new MarkShipmentDeliveredCommand(shipmentId), cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
