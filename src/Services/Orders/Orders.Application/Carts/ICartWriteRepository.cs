using Luna.Orders.Domain;

namespace Luna.Orders.Application.Carts;

public interface ICartWriteRepository
{
    Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
