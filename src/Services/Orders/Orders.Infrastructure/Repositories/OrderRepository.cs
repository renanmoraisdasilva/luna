using Luna.Orders.Application.Checkout;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Database;

namespace Luna.Orders.Infrastructure.Repositories;

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderWriteRepository
{
    public Task AddAsync(Order order, CancellationToken cancellationToken) =>
        dbContext.Orders.AddAsync(order, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
