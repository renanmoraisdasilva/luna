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
}
