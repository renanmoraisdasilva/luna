using Luna.Orders.Application.Checkout;
using Luna.Orders.Application.Orders;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Luna.Orders.Infrastructure.Repositories;

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderWriteRepository
{
    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders.SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.Orders.SingleOrDefaultAsync(order => order.CustomerId == customerId && order.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task AddAsync(Order order, CancellationToken cancellationToken) =>
        dbContext.Orders.AddAsync(order, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
