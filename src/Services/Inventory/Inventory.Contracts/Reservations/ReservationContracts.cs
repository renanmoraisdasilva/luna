namespace Luna.Inventory.Contracts.Reservations;

public sealed record ReserveInventoryRequest(
    Guid OrderId,
    IReadOnlyCollection<ReserveInventoryItem> Items);

public sealed record ReserveInventoryItem(Guid ProductId, int Quantity);

public sealed record ReservationResponse(Guid ReservationId, Guid OrderId, string Status);

public sealed record ReleaseReservationResponse(Guid ReservationId, Guid OrderId, string Status);
