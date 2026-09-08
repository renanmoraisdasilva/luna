using Luna.Inventory.Domain;

namespace Luna.Inventory.Application.Reservations;

public interface IInventoryRepository
{
    Task<InventoryReservation> ReserveAsync(
        Guid orderId,
        IReadOnlyCollection<ReservationItem> items,
        CancellationToken cancellationToken);

    Task<InventoryReservation> ReleaseAsync(
        Guid reservationId,
        CancellationToken cancellationToken);
}

public sealed record ReservationItem(Guid ProductId, int Quantity);