using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Contracts.Reservations;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Inventory.Api.Controllers;

[ApiController]
[Route("api/v1/reservations")]
public sealed class ReservationsController(
    ReserveInventoryHandler reserveInventory,
    ReleaseReservationHandler releaseReservation) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LunaServicePolicies.OrdersInventoryReservationsWrite)]
    public async Task<ActionResult<ReservationResponse>> Reserve(
        ReserveInventoryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await reserveInventory.HandleAsync(
            new ReserveInventoryCommand(request.OrderId, request.Items),
            cancellationToken));
    }

    [HttpPost("{id:guid}/release")]
    [Authorize(Policy = LunaServicePolicies.OrdersInventoryReservationsWrite)]
    public async Task<ActionResult<ReleaseReservationResponse>> Release(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await releaseReservation.HandleAsync(
            new ReleaseReservationCommand(id),
            cancellationToken));
    }
}
