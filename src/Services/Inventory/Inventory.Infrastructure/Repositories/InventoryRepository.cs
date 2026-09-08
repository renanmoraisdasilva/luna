using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Inventory.Infrastructure.Repositories;

public sealed class InventoryRepository(InventoryDbContext dbContext) : IInventoryRepository
{
    public async Task<InventoryReservation> ReserveAsync(
        Guid orderId,
        IReadOnlyCollection<ReservationItem> items,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken);

        foreach (var item in items)
        {
            await ReserveStockAsync(item, cancellationToken);
        }

        var reservation = InventoryReservation.Create(
            orderId,
            items.Select(item => (item.ProductId, item.Quantity)).ToArray());

        dbContext.InventoryReservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return reservation;
    }

    public async Task<InventoryReservation> ReleaseAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken);

        var reservation = await ReleaseReservationAsync(reservationId, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "The reservation was modified concurrently.");
        }

        return reservation;
    }

    private async Task ReserveStockAsync(
        ReservationItem item,
        CancellationToken cancellationToken)
    {
        var affectedRows = await dbContext.Stocks
            .Where(stock =>
                stock.ProductId == item.ProductId &&
                stock.QuantityOnHand - stock.QuantityReserved >= item.Quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    stock => stock.QuantityReserved,
                    stock => stock.QuantityReserved + item.Quantity),
                cancellationToken);

        if (affectedRows == 1)
        {
            return;
        }

        var stock = await dbContext.Stocks
            .SingleOrDefaultAsync(stock => stock.ProductId == item.ProductId, cancellationToken);
        if (stock is null)
        {
            throw new StockNotFoundException(item.ProductId);
        }

        throw new InsufficientInventoryException(
            item.ProductId,
            item.Quantity,
            stock.AvailableQuantity);
    }

    private async Task<InventoryReservation> ReleaseReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.InventoryReservations
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken)
            ?? throw new KeyNotFoundException("The inventory reservation was not found.");

        if (reservation.Status == InventoryReservationStatus.Released)
        {
            return reservation;
        }

        var productIds = reservation.Items.Select(item => item.ProductId).ToArray();
        var stocks = await dbContext.Stocks
            .Where(stock => productIds.Contains(stock.ProductId))
            .ToDictionaryAsync(stock => stock.ProductId, cancellationToken);

        foreach (var item in reservation.Items)
        {
            if (!stocks.TryGetValue(item.ProductId, out var stock))
            {
                throw new StockNotFoundException(item.ProductId);
            }

            stock.Release(item.Quantity);
        }

        reservation.Release();
        return reservation;
    }
}
