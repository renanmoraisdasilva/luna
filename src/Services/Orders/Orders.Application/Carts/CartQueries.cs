using Luna.Orders.Contracts.Carts;

namespace Luna.Orders.Application.Carts;

public sealed class GetCartHandler(ICartReadRepository repository)
{
    public async Task<CartResponse> HandleAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await repository.GetCartAsync(customerId, cancellationToken)
            ?? new CartResponse(Guid.Empty, customerId, []);
    }
}
