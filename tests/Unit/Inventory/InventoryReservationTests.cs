using FluentAssertions;
using Luna.Inventory.Domain;
using Xunit;

namespace Luna.UnitTests.Inventory;

public sealed class InventoryReservationTests
{
    [Fact]
    public void Release_changes_the_reservation_status()
    {
        var reservation = InventoryReservation.Create(
            Guid.NewGuid(),
            [(Guid.NewGuid(), 2)]);

        reservation.Release();

        reservation.Status.Should().Be(InventoryReservationStatus.Released);
    }

    [Fact]
    public void Create_rejects_an_empty_order_id()
    {
        var act = () => InventoryReservation.Create(Guid.Empty, [(Guid.NewGuid(), 2)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_requires_at_least_one_item()
    {
        var act = () => InventoryReservation.Create(Guid.NewGuid(), []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_uses_the_supplied_id()
    {
        var id = Guid.NewGuid();

        var reservation = InventoryReservation.Create(id, [(Guid.NewGuid(), 2)], id);

        reservation.Id.Should().Be(id);
    }

    [Theory]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(false, false, 0)]
    public void Reservation_item_validates_ids_and_quantity(bool emptyReservationId, bool emptyProductId, int quantity)
    {
        var act = () => InventoryReservationItem.Create(
            emptyReservationId ? Guid.Empty : Guid.NewGuid(),
            emptyProductId ? Guid.Empty : Guid.NewGuid(),
            quantity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reservation_item_uses_the_supplied_id()
    {
        var id = Guid.NewGuid();

        var item = InventoryReservationItem.Create(Guid.NewGuid(), Guid.NewGuid(), 2, id);

        item.Id.Should().Be(id);
    }
}
