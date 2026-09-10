using FluentAssertions;
using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Contracts.Reservations;
using Luna.Inventory.Domain;
using Xunit;

namespace Luna.UnitTests.Inventory;

public sealed class ReservationHandlerTests
{
    [Fact]
    public async Task Reserve_groups_duplicate_products_and_maps_the_reservation()
    {
        var repository = new FakeRepository();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var handler = new ReserveInventoryHandler(repository);

        var response = await handler.HandleAsync(
            new ReserveInventoryCommand(orderId, [new(productId, 2), new(productId, 3)]),
            CancellationToken.None);

        response.OrderId.Should().Be(orderId);
        response.Status.Should().Be(nameof(InventoryReservationStatus.Active));
        repository.ReservedItems.Should().ContainSingle(item => item.ProductId == productId && item.Quantity == 5);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Reserve_rejects_invalid_commands(bool emptyOrder, bool emptyItems, bool invalidItem)
    {
        var items = emptyItems
            ? Array.Empty<ReserveInventoryItem>()
            : [new(invalidItem ? Guid.Empty : Guid.NewGuid(), invalidItem ? 0 : 1)];
        var handler = new ReserveInventoryHandler(new FakeRepository());

        var act = () => handler.HandleAsync(
            new ReserveInventoryCommand(emptyOrder ? Guid.Empty : Guid.NewGuid(), items),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Release_maps_the_released_reservation()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), [(Guid.NewGuid(), 1)]);
        reservation.Release();
        var repository = new FakeRepository { Released = reservation };

        var response = await new ReleaseReservationHandler(repository)
            .HandleAsync(new ReleaseReservationCommand(reservation.Id), CancellationToken.None);

        response.ReservationId.Should().Be(reservation.Id);
        response.Status.Should().Be(nameof(InventoryReservationStatus.Released));
    }

    private sealed class FakeRepository : IInventoryRepository
    {
        public IReadOnlyCollection<ReservationItem> ReservedItems { get; private set; } = [];
        public InventoryReservation? Released { get; init; }

        public Task<InventoryReservation> ReserveAsync(Guid orderId, IReadOnlyCollection<ReservationItem> items, CancellationToken cancellationToken)
        {
            ReservedItems = items;
            return Task.FromResult(InventoryReservation.Create(orderId, items.Select(item => (item.ProductId, item.Quantity)).ToArray()));
        }

        public Task<InventoryReservation> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult(Released ?? InventoryReservation.Create(Guid.NewGuid(), [(Guid.NewGuid(), 1)], reservationId));
    }
}
