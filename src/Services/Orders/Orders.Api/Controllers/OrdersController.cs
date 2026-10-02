using Luna.Authentication;
using Luna.Orders.Application.Orders;
using Luna.Orders.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Luna.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Orders.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = LunaAuthenticationDefaults.ValidationScheme)]
[Route("api/v1/orders")]
public sealed class OrdersController(IOrderReadRepository orders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OrderSummaryResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!User.TryGetSubjectId(out var customerId))
        {
            throw new UnauthenticatedCustomerException();
        }

        return Ok(await orders.GetByCustomerIdAsync(customerId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetSubjectId(out var customerId))
        {
            throw new UnauthenticatedCustomerException();
        }

        var order = await orders.GetByIdAsync(customerId, id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
