using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;

namespace Luna.Orders.Application.Carts;

public sealed class CartService(ICartRepository cartRepository)
{
    public async Task<CartResponse> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(customerId, cancellationToken);
        return ToResponse(cart);
    }

    public async Task<CartResponse> AddItemAsync(Guid customerId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var cart = await GetOrCreateAsync(customerId, cancellationToken);
        cart.AddItem(productId, quantity);
        await cartRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(cart);
    }

    public async Task<CartResponse> ChangeItemQuantityAsync(Guid customerId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var cart = await GetExistingAsync(customerId, cancellationToken);
        cart.ChangeItemQuantity(productId, quantity);
        await cartRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(cart);
    }

    public async Task RemoveItemAsync(Guid customerId, Guid productId, CancellationToken cancellationToken)
    {
        var cart = await GetExistingAsync(customerId, cancellationToken);
        cart.RemoveItem(productId);
        await cartRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken)
        => await cartRepository.GetOrCreateAsync(customerId, cancellationToken);

    private async Task<Cart> GetExistingAsync(Guid customerId, CancellationToken cancellationToken) =>
        await cartRepository.GetByCustomerIdAsync(customerId, cancellationToken)
        ?? throw new KeyNotFoundException("The customer does not have a cart.");

    private static CartResponse ToResponse(Cart cart) =>
        new(cart.Id, cart.CustomerId, cart.Items
            .Select(item => new CartItemResponse(item.ProductId, item.Quantity))
            .ToArray());
}
