using Luna.Inventory.Contracts.Reservations;
using Luna.Inventory.Domain;

namespace Luna.Inventory.Application.Reservations;

public sealed record ReserveInventoryCommand(
    Guid OrderId,
    IReadOnlyCollection<ReserveInventoryItem> Items);

public sealed record ReleaseReservationCommand(Guid ReservationId);

public sealed class ReserveInventoryHandler(IInventoryRepository repository)
{
    public async Task<ReservationResponse> HandleAsync(
        ReserveInventoryCommand command,
        CancellationToken cancellationToken)
    {
        var items = command.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new ReservationItem(group.Key, group.Sum(item => item.Quantity)))
            .ToArray();

        if (command.OrderId == Guid.Empty || items.Length == 0)
        {
            throw new ArgumentException("An order and at least one inventory item are required.");
        }

        if (items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            throw new ArgumentException("Product IDs and quantities must be valid.");
        }

        var reservation = await repository.ReserveAsync(command.OrderId, items, cancellationToken);
        return ReservationMapper.Map(reservation);
    }
}

public sealed class ReleaseReservationHandler(IInventoryRepository repository)
{
    public async Task<ReleaseReservationResponse> HandleAsync(
        ReleaseReservationCommand command,
        CancellationToken cancellationToken)
    {
        var reservation = await repository.ReleaseAsync(command.ReservationId, cancellationToken);
        return ReservationMapper.MapRelease(reservation);
    }
}

internal static class ReservationMapper
{
    public static ReservationResponse Map(InventoryReservation reservation) =>
        new(reservation.Id, reservation.OrderId, reservation.Status.ToString());

    public static ReleaseReservationResponse MapRelease(InventoryReservation reservation) =>
        new(reservation.Id, reservation.OrderId, reservation.Status.ToString());
}