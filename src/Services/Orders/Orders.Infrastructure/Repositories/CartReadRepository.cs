using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Luna.Orders.Infrastructure.Repositories;

public sealed class CartReadRepository(OrdersDbContext dbContext) : ICartReadRepository
{
    public Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Carts
            .AsNoTracking()
            .Where(cart => cart.CustomerId == customerId)
            .Select(cart => new CartResponse(
                cart.Id,
                cart.CustomerId,
                cart.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new CartItemResponse(item.ProductId, item.Quantity))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
}
