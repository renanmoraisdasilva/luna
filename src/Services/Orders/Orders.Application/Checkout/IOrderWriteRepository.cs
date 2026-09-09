using Luna.Orders.Domain;

namespace Luna.Orders.Application.Checkout;

public interface IOrderWriteRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
