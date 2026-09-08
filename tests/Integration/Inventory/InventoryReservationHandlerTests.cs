using FluentAssertions;
using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Contracts.Reservations;
using Luna.Inventory.Domain;
using Luna.Inventory.Infrastructure.Repositories;
using Luna.Inventory.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Inventory;

[Collection(InventoryDatabaseCollection.Name)]
public sealed class InventoryReservationHandlerTests(InventorySqlServerFixture fixture)
{
    [Fact]
    public async Task Seed_creates_stock_for_every_catalog_product_and_is_idempotent()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateDbContext();

        await InventorySeed.SeedAsync(db);
        await InventorySeed.SeedAsync(db);

        var stocks = await db.Stocks.ToListAsync();
        stocks.Should().HaveCount(22);
        stocks.Should().OnlyContain(stock => stock.QuantityOnHand == 100 && stock.QuantityReserved == 0);
        stocks.Select(stock => stock.ProductId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Reserves_and_releases_inventory_using_persisted_state()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 5));
            await seedDb.SaveChangesAsync();
        }

        Guid reservationId;
        await using (var db = fixture.CreateDbContext())
        {
            var handler = new ReserveInventoryHandler(new InventoryRepository(db));
            var result = await handler.HandleAsync(
                new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(productId, 3)]),
                CancellationToken.None);
            reservationId = result.ReservationId;
        }

        await using (var db = fixture.CreateDbContext())
        {
            var stock = await db.Stocks.SingleAsync(item => item.ProductId == productId);
            stock.AvailableQuantity.Should().Be(2);
            await new ReleaseReservationHandler(new InventoryRepository(db))
                .HandleAsync(new ReleaseReservationCommand(reservationId), CancellationToken.None);
        }

        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Stocks.SingleAsync(item => item.ProductId == productId)).AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public async Task Reports_actual_available_quantity_when_inventory_is_insufficient()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 5));
            await seedDb.SaveChangesAsync();
        }

        await using var db = fixture.CreateDbContext();
        var handler = new ReserveInventoryHandler(new InventoryRepository(db));

        var act = () => handler.HandleAsync(
            new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(productId, 6)]),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InsufficientInventoryException>();
        exception.Which.Available.Should().Be(5);
    }

    [Fact]
    public async Task Distinguishes_missing_stock_from_insufficient_inventory()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateDbContext();
        var handler = new ReserveInventoryHandler(new InventoryRepository(db));

        var act = () => handler.HandleAsync(
            new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(Guid.NewGuid(), 1)]),
            CancellationToken.None);

        await act.Should().ThrowAsync<StockNotFoundException>();
    }

    [Fact]
    public async Task Failed_multi_item_reservation_rolls_back_previous_stock_updates()
    {
        await fixture.ResetAsync();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.AddRange(
                Stock.Create(firstProductId, 5),
                Stock.Create(secondProductId, 1));
            await seedDb.SaveChangesAsync();
        }

        await using var db = fixture.CreateDbContext();
        var handler = new ReserveInventoryHandler(new InventoryRepository(db));
        var act = () => handler.HandleAsync(
            new ReserveInventoryCommand(
                Guid.NewGuid(),
                [
                    new ReserveInventoryItem(firstProductId, 3),
                    new ReserveInventoryItem(secondProductId, 2)
                ]),
            CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientInventoryException>();
        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Stocks.SingleAsync(stock => stock.ProductId == firstProductId)).QuantityReserved.Should().Be(0);
        (await verificationDb.Stocks.SingleAsync(stock => stock.ProductId == secondProductId)).QuantityReserved.Should().Be(0);
        (await verificationDb.InventoryReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Releasing_an_already_released_reservation_is_idempotent()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 5));
            await seedDb.SaveChangesAsync();
        }

        Guid reservationId;
        await using (var db = fixture.CreateDbContext())
        {
            var reservation = await new ReserveInventoryHandler(new InventoryRepository(db)).HandleAsync(
                new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(productId, 3)]),
                CancellationToken.None);
            reservationId = reservation.ReservationId;
        }

        await using (var firstReleaseDb = fixture.CreateDbContext())
        {
            await new ReleaseReservationHandler(new InventoryRepository(firstReleaseDb))
                .HandleAsync(new ReleaseReservationCommand(reservationId), CancellationToken.None);
        }

        await using (var secondReleaseDb = fixture.CreateDbContext())
        {
            var result = await new ReleaseReservationHandler(new InventoryRepository(secondReleaseDb))
                .HandleAsync(new ReleaseReservationCommand(reservationId), CancellationToken.None);
            result.Status.Should().Be(InventoryReservationStatus.Released.ToString());
        }

        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Stocks.SingleAsync(stock => stock.ProductId == productId)).QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_releases_do_not_release_the_same_stock_twice()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 5));
            await seedDb.SaveChangesAsync();
        }

        Guid reservationId;
        await using (var db = fixture.CreateDbContext())
        {
            var reservation = await new ReserveInventoryHandler(new InventoryRepository(db)).HandleAsync(
                new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(productId, 3)]),
                CancellationToken.None);
            reservationId = reservation.ReservationId;
        }

        var results = await Task.WhenAll(ReleaseAsync(), ReleaseAsync());

        results.Count(result => result).Should().BeGreaterThanOrEqualTo(1);
        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Stocks.SingleAsync(stock => stock.ProductId == productId)).QuantityReserved.Should().Be(0);

        async Task<bool> ReleaseAsync()
        {
            await using var db = fixture.CreateDbContext();
            try
            {
                await new ReleaseReservationHandler(new InventoryRepository(db))
                    .HandleAsync(new ReleaseReservationCommand(reservationId), CancellationToken.None);
                return true;
            }
            catch (InvalidOperationException exception) when (exception.Message == "The reservation was modified concurrently.")
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task Reservation_row_version_detects_concurrent_state_changes()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 5));
            await seedDb.SaveChangesAsync();
        }

        Guid reservationId;
        await using (var db = fixture.CreateDbContext())
        {
            var reservation = await new ReserveInventoryHandler(new InventoryRepository(db)).HandleAsync(
                new ReserveInventoryCommand(Guid.NewGuid(), [new ReserveInventoryItem(productId, 3)]),
                CancellationToken.None);
            reservationId = reservation.ReservationId;
        }

        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var firstReservation = await firstDb.InventoryReservations.SingleAsync(
            reservation => reservation.Id == reservationId);
        var secondReservation = await secondDb.InventoryReservations.SingleAsync(
            reservation => reservation.Id == reservationId);

        firstReservation.Release();
        secondReservation.Release();
        await firstDb.SaveChangesAsync();

        var act = () => secondDb.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Concurrent_reservations_cannot_overbook_one_available_unit()
    {
        await fixture.ResetAsync();
        var productId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.Stocks.Add(Stock.Create(productId, 1));
            await seedDb.SaveChangesAsync();
        }

        var firstTask = ReserveAsync(Guid.NewGuid());
        var secondTask = ReserveAsync(Guid.NewGuid());
        var results = await Task.WhenAll(firstTask, secondTask);

        results.Count(result => result).Should().Be(1);
        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Stocks.SingleAsync(item => item.ProductId == productId)).QuantityReserved.Should().Be(1);

        async Task<bool> ReserveAsync(Guid orderId)
        {
            await using var db = fixture.CreateDbContext();
            try
            {
                await new ReserveInventoryHandler(new InventoryRepository(db)).HandleAsync(
                    new ReserveInventoryCommand(orderId, [new ReserveInventoryItem(productId, 1)]),
                    CancellationToken.None);
                return true;
            }
            catch (InsufficientInventoryException)
            {
                return false;
            }
        }
    }
}
