using Luna.Orders.Application.Carts;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Luna.Orders.Infrastructure.Repositories;

public sealed class CartRepository(OrdersDbContext dbContext) : ICartWriteRepository
{
    public Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Carts
            .Include(cart => cart.Items)
            .SingleOrDefaultAsync(cart => cart.CustomerId == customerId, cancellationToken);

    public async Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var existingCart = await GetByCustomerIdAsync(customerId, cancellationToken);
        if (existingCart is not null)
        {
            return existingCart;
        }

        var newCart = Cart.Create(customerId);
        await dbContext.Carts.AddAsync(newCart, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return newCart;
        }
        catch (DbUpdateException exception) when (IsDuplicateCustomerCart(exception))
        {
            dbContext.Entry(newCart).State = EntityState.Detached;

            return await GetByCustomerIdAsync(customerId, cancellationToken)
                ?? throw new InvalidOperationException("The cart was created concurrently but could not be loaded.");
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private static bool IsDuplicateCustomerCart(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Number is 2601 or 2627;
}
