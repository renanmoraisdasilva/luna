using FluentAssertions;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShipmentTests
{
    [Fact]
    public void Shipment_transitions_from_created_to_in_transit_to_delivered()
    {
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "LUNA-123", Recipient());

        shipment.MarkInTransit();
        shipment.MarkDelivered();

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.DeliveredAt.Should().NotBeNull();
        shipment.TrackingEvents.Select(trackingEvent => trackingEvent.Status)
            .Should().Equal("Created", "InTransit", "Delivered");
    }

    [Fact]
    public void Shipment_cannot_be_delivered_before_in_transit()
    {
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "LUNA-123", Recipient());

        var act = () => shipment.MarkDelivered();

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Shipment_requires_order_and_quote_ids(bool emptyOrderId, bool emptyQuoteId)
    {
        var act = () => Shipment.Create(
            emptyOrderId ? Guid.Empty : Guid.NewGuid(),
            emptyQuoteId ? Guid.Empty : Guid.NewGuid(),
            "LUNA-123",
            Recipient());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipment_requires_a_tracking_number()
    {
        var act = () => Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), " ", Recipient());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipment_uses_supplied_id_and_delivery_time()
    {
        var id = Guid.NewGuid();
        var deliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), " LUNA-123 ", Recipient(), id);

        shipment.MarkInTransit();
        shipment.MarkDelivered(deliveredAt);

        shipment.Id.Should().Be(id);
        shipment.TrackingNumber.Should().Be("LUNA-123");
        shipment.DeliveredAt.Should().Be(deliveredAt);
    }

    [Fact]
    public void Shipment_keeps_the_recipient_snapshot()
    {
        var customerId = Guid.NewGuid();
        var shipment = Shipment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LUNA-123",
            ShipmentRecipientSnapshot.Create(customerId, " Jane Doe ", " 1 Main Street ", null, " Austin ", " Texas ", " 78701 ", " US "));

        shipment.Recipient.CustomerId.Should().Be(customerId);
        shipment.Recipient.FullName.Should().Be("Jane Doe");
        shipment.Recipient.AddressLine1.Should().Be("1 Main Street");
        shipment.Recipient.City.Should().Be("Austin");
    }

    private static ShipmentRecipientSnapshot Recipient() => ShipmentRecipientSnapshot.Create(
        Guid.NewGuid(),
        "Jane Doe",
        "1 Main Street",
        null,
        "Austin",
        "Texas",
        "78701",
        "US");

    [Theory]
    [InlineData(true, "InTransit")]
    [InlineData(false, "")]
    public void Tracking_event_requires_an_id_and_status(bool emptyId, string status)
    {
        var act = () => TrackingEvent.Create(
            emptyId ? Guid.Empty : Guid.NewGuid(),
            status);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tracking_event_uses_supplied_id_and_time()
    {
        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var trackingEvent = TrackingEvent.Create(Guid.NewGuid(), "InTransit", occurredAt, id);

        trackingEvent.Id.Should().Be(id);
        trackingEvent.OccurredAt.Should().Be(occurredAt);
    }
}
