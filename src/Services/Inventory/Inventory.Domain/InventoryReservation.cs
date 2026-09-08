namespace Luna.Inventory.Domain;

public sealed class InventoryReservation
{
    private readonly List<InventoryReservationItem> items = [];

    private InventoryReservation()
    {
    }

    private InventoryReservation(Guid id, Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items)
    {
        Id = id;
        OrderId = orderId;
        this.items.AddRange(items
            .Select(item => InventoryReservationItem.Create(id, item.ProductId, item.Quantity))
            .ToList());
        Status = InventoryReservationStatus.Active;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public InventoryReservationStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<InventoryReservationItem> Items => items.AsReadOnly();

    public static InventoryReservation Create(Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, Guid? id = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(orderId));
        }

        if (items.Count == 0)
        {
            throw new ArgumentException("At least one reservation item is required.", nameof(items));
        }

        return new InventoryReservation(id ?? Guid.NewGuid(), orderId, items);
    }

    public void Release()
    {
        Status = InventoryReservationStatus.Released;
    }
}

public sealed class InventoryReservationItem
{
    private InventoryReservationItem()
    {
    }

    private InventoryReservationItem(Guid id, Guid reservationId, Guid productId, int quantity)
    {
        Id = id;
        ReservationId = reservationId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    public static InventoryReservationItem Create(Guid reservationId, Guid productId, int quantity, Guid? id = null)
    {
        if (reservationId == Guid.Empty || productId == Guid.Empty)
        {
            throw new ArgumentException("Reservation and product IDs are required.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        return new InventoryReservationItem(id ?? Guid.NewGuid(), reservationId, productId, quantity);
    }
}

public enum InventoryReservationStatus
{
    Active,
    Released
}
