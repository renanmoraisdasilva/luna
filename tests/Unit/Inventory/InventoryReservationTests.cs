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
}
