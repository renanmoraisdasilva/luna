using System.Security.Claims;
using Luna.Authentication;
using Luna.Contracts.Authentication;
using Luna.Orders.Api.Authorization;
using Luna.Orders.Application.Orders;
using Luna.Orders.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Orders.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme, Policy = OrdersAuthorizationPolicies.Operations)]
[Route("api/v1/orders/fulfillment")]
public sealed class FulfillmentController : ControllerBase
{
    private readonly GetFulfillmentQueueHandler getQueue;
    private readonly GetFulfillmentOrderHandler getOrder;
    private readonly PrepareFulfillmentOrderHandler prepareOrder;
    private readonly CreateShipmentHandler createShipment;
    private readonly MarkShipmentInTransitHandler markShipmentInTransit;
    private readonly MarkShipmentDeliveredHandler markShipmentDelivered;

    public FulfillmentController(
        GetFulfillmentQueueHandler getQueue,
        GetFulfillmentOrderHandler getOrder,
        PrepareFulfillmentOrderHandler prepareOrder,
        CreateShipmentHandler createShipment,
        MarkShipmentInTransitHandler markShipmentInTransit,
        MarkShipmentDeliveredHandler markShipmentDelivered)
    {
        this.getQueue = getQueue;
        this.getOrder = getOrder;
        this.prepareOrder = prepareOrder;
        this.createShipment = createShipment;
        this.markShipmentInTransit = markShipmentInTransit;
        this.markShipmentDelivered = markShipmentDelivered;
    }

    [HttpGet]
    [ProducesResponseType<FulfillmentQueueResponse>(StatusCodes.Status200OK)]
    public Task<FulfillmentQueueResponse> GetQueue(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        getQueue.HandleAsync(
            new FulfillmentQueueQuery(status, search, page, pageSize),
            cancellationToken);

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<FulfillmentOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FulfillmentOrderResponse>> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await getOrder.HandleAsync(orderId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{orderId:guid}/prepare")]
    [ProducesResponseType<FulfillmentCommandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfillmentCommandResponse>> Prepare(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var response = await prepareOrder.HandleAsync(
            new PrepareFulfillmentOrderCommand(orderId),
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{orderId:guid}/shipment")]
    [ProducesResponseType<FulfillmentCommandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfillmentCommandResponse>> CreateShipment(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var response = await createShipment.HandleAsync(
            new CreateShipmentCommand(orderId),
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("shipments/{shipmentId:guid}/in-transit")]
    [ProducesResponseType<FulfillmentCommandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfillmentCommandResponse>> MarkShipmentInTransit(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await markShipmentInTransit.HandleAsync(
            new MarkShipmentInTransitCommand(shipmentId),
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("shipments/{shipmentId:guid}/delivered")]
    [ProducesResponseType<FulfillmentCommandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FulfillmentCommandResponse>> MarkShipmentDelivered(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var response = await markShipmentDelivered.HandleAsync(
            new MarkShipmentDeliveredCommand(shipmentId),
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
