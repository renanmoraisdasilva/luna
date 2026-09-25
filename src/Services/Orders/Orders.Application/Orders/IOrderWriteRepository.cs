using Luna.Orders.Domain;

namespace Luna.Orders.Application.Orders;

public interface IOrderWriteRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken);
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
