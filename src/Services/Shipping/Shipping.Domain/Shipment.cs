namespace Luna.Shipping.Domain;

public sealed class Shipment
{
    private readonly List<TrackingEvent> trackingEvents = [];

    private Shipment()
    {
    }

    private Shipment(Guid id, Guid orderId, Guid quoteId, string trackingNumber, ShipmentRecipientSnapshot recipient)
    {
        Id = id;
        OrderId = orderId;
        QuoteId = quoteId;
        TrackingNumber = trackingNumber;
        Recipient = recipient;
        Status = ShipmentStatus.Created;
        trackingEvents.Add(TrackingEvent.Create(id, ShipmentStatus.Created.ToString()));
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid QuoteId { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public ShipmentRecipientSnapshot Recipient { get; private set; } = null!;
    public ShipmentStatus Status { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public IReadOnlyCollection<TrackingEvent> TrackingEvents => trackingEvents.AsReadOnly();

    public static Shipment Create(
        Guid orderId,
        Guid quoteId,
        string trackingNumber,
        ShipmentRecipientSnapshot recipient,
        Guid? id = null)
    {
        if (orderId == Guid.Empty || quoteId == Guid.Empty)
        {
            throw new ArgumentException("Order and quote IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            throw new ArgumentException("Tracking number is required.", nameof(trackingNumber));
        }

        ArgumentNullException.ThrowIfNull(recipient);
        return new Shipment(id ?? Guid.NewGuid(), orderId, quoteId, trackingNumber.Trim(), recipient);
    }

    public void MarkInTransit()
    {
        EnsureStatus(ShipmentStatus.Created);
        Status = ShipmentStatus.InTransit;
        trackingEvents.Add(TrackingEvent.Create(Id, ShipmentStatus.InTransit.ToString()));
    }

    public void MarkDelivered(DateTimeOffset? deliveredAt = null)
    {
        EnsureStatus(ShipmentStatus.InTransit);
        Status = ShipmentStatus.Delivered;
        DeliveredAt = deliveredAt ?? DateTimeOffset.UtcNow;
        trackingEvents.Add(TrackingEvent.Create(Id, ShipmentStatus.Delivered.ToString(), DeliveredAt.Value));
    }

    private void EnsureStatus(ShipmentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Shipment must be {expected} before it can transition from {Status}.");
        }
    }
}

public enum ShipmentStatus
{
    Created,
    InTransit,
    Delivered
}

public sealed class TrackingEvent
{
    private TrackingEvent()
    {
    }

    private TrackingEvent(Guid id, Guid shipmentId, string status, DateTimeOffset occurredAt)
    {
        Id = id;
        ShipmentId = shipmentId;
        Status = status;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    public static TrackingEvent Create(
        Guid shipmentId,
        string status,
        DateTimeOffset? occurredAt = null,
        Guid? id = null)
    {
        if (shipmentId == Guid.Empty || string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Shipment ID and tracking status are required.");
        }

        return new TrackingEvent(id ?? Guid.NewGuid(), shipmentId, status, occurredAt ?? DateTimeOffset.UtcNow);
    }
}
