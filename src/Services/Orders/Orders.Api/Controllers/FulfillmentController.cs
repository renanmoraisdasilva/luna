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

    public FulfillmentController(
        GetFulfillmentQueueHandler getQueue,
        GetFulfillmentOrderHandler getOrder,
        PrepareFulfillmentOrderHandler prepareOrder)
    {
        this.getQueue = getQueue;
        this.getOrder = getOrder;
        this.prepareOrder = prepareOrder;
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
}
