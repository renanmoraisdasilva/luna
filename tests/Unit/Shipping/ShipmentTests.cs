using FluentAssertions;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShipmentTests
{
    [Fact]
    public void Shipment_transitions_from_created_to_in_transit_to_delivered()
    {
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "LUNA-123");

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
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), "LUNA-123");

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
            "LUNA-123");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipment_requires_a_tracking_number()
    {
        var act = () => Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipment_uses_supplied_id_and_delivery_time()
    {
        var id = Guid.NewGuid();
        var deliveredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var shipment = Shipment.Create(Guid.NewGuid(), Guid.NewGuid(), " LUNA-123 ", id);

        shipment.MarkInTransit();
        shipment.MarkDelivered(deliveredAt);

        shipment.Id.Should().Be(id);
        shipment.TrackingNumber.Should().Be("LUNA-123");
        shipment.DeliveredAt.Should().Be(deliveredAt);
    }

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
