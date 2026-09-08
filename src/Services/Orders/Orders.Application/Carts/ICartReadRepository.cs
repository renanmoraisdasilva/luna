using Luna.Orders.Contracts.Carts;

namespace Luna.Orders.Application.Carts;

public interface ICartReadRepository
{
    Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken);
}
