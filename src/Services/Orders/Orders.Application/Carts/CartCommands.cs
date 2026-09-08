using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;

namespace Luna.Orders.Application.Carts;

public sealed record AddCartItemCommand(Guid CustomerId, Guid ProductId, int Quantity);

public sealed record ChangeCartItemQuantityCommand(Guid CustomerId, Guid ProductId, int Quantity);

public sealed record RemoveCartItemCommand(Guid CustomerId, Guid ProductId);

public sealed class AddCartItemHandler(ICartWriteRepository repository)
{
    public async Task<CartResponse> HandleAsync(AddCartItemCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetOrCreateAsync(command.CustomerId, cancellationToken);
        cart.AddItem(command.ProductId, command.Quantity);
        await repository.SaveChangesAsync(cancellationToken);
        return CartResponseMapper.Map(cart);
    }
}

public sealed class ChangeCartItemQuantityHandler(ICartWriteRepository repository)
{
    public async Task<CartResponse> HandleAsync(ChangeCartItemQuantityCommand command, CancellationToken cancellationToken)
    {
        var cart = await GetExistingAsync(command.CustomerId, cancellationToken);
        cart.ChangeItemQuantity(command.ProductId, command.Quantity);
        await repository.SaveChangesAsync(cancellationToken);
        return CartResponseMapper.Map(cart);
    }

    private async Task<Cart> GetExistingAsync(Guid customerId, CancellationToken cancellationToken) =>
        await repository.GetByCustomerIdAsync(customerId, cancellationToken)
        ?? throw new KeyNotFoundException("The customer does not have a cart.");
}

public sealed class RemoveCartItemHandler(ICartWriteRepository repository)
{
    public async Task HandleAsync(RemoveCartItemCommand command, CancellationToken cancellationToken)
    {
        var cart = await repository.GetByCustomerIdAsync(command.CustomerId, cancellationToken)
            ?? throw new KeyNotFoundException("The customer does not have a cart.");

        cart.RemoveItem(command.ProductId);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

internal static class CartResponseMapper
{
    public static CartResponse Map(Cart cart) =>
        new(cart.Id, cart.CustomerId, cart.Items
            .Select(item => new CartItemResponse(item.ProductId, item.Quantity))
            .ToArray());
}
