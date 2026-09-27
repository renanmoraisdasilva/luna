using FluentAssertions;
using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShipmentResponseMapperTests
{
    [Theory]
    [InlineData(ShipmentStatus.Created, "MarkInTransit")]
    [InlineData(ShipmentStatus.InTransit, "MarkDelivered")]
    [InlineData(ShipmentStatus.Delivered, "None")]
    public void Available_action_reflects_the_shipment_status(ShipmentStatus status, string expectedAction)
    {
        ShipmentResponseMapper.AvailableAction(status).Should().Be(expectedAction);
    }

    [Fact]
    public void Map_projects_the_shipment_and_its_recipient()
    {
        var shipment = Shipment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LUNA-MAP-001",
            ShipmentRecipientSnapshot.Create(
                Guid.NewGuid(),
                "Jane Doe",
                "1 Main Street",
                "Suite 2",
                "Austin",
                "Texas",
                "78701",
                "US"));

        var response = ShipmentResponseMapper.Map(shipment);

        response.ShipmentId.Should().Be(shipment.Id);
        response.OrderId.Should().Be(shipment.OrderId);
        response.QuoteId.Should().Be(shipment.QuoteId);
        response.Status.Should().Be(nameof(ShipmentStatus.Created));
        response.TrackingNumber.Should().Be("LUNA-MAP-001");
        response.DeliveredAt.Should().BeNull();
        response.Recipient.AddressLine2.Should().Be("Suite 2");
    }
}
