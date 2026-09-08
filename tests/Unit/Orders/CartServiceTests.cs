using FluentAssertions;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class CartCommandHandlerTests
{
    [Fact]
    public async Task Add_item_creates_cart_and_returns_cart_contents()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new InMemoryCartRepository();

        var result = await new AddCartItemHandler(repository).HandleAsync(
            new AddCartItemCommand(customerId, productId, 2),
            CancellationToken.None);

        result.CustomerId.Should().Be(customerId);
        result.Items.Should().ContainSingle().Which.Should().Be(new CartItemResponse(productId, 2));
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Remove_item_does_not_call_catalog_or_reserve_inventory()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId);
        cart.AddItem(productId, 1);
        var repository = new InMemoryCartRepository(cart);

        await new RemoveCartItemHandler(repository).HandleAsync(
            new RemoveCartItemCommand(customerId, productId),
            CancellationToken.None);

        cart.Items.Should().BeEmpty();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Change_quantity_updates_an_existing_item()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId);
        cart.AddItem(productId, 1);
        var repository = new InMemoryCartRepository(cart);

        var result = await new ChangeCartItemQuantityHandler(repository).HandleAsync(
            new ChangeCartItemQuantityCommand(customerId, productId, 4),
            CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Quantity.Should().Be(4);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Change_quantity_rejects_a_customer_without_a_cart()
    {
        var repository = new InMemoryCartRepository();

        var act = () => new ChangeCartItemQuantityHandler(repository).HandleAsync(
            new ChangeCartItemQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), 2),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("The customer does not have a cart.");
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Remove_item_rejects_a_customer_without_a_cart()
    {
        var repository = new InMemoryCartRepository();

        var act = () => new RemoveCartItemHandler(repository).HandleAsync(
            new RemoveCartItemCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("The customer does not have a cart.");
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Remove_item_rejects_a_product_missing_from_the_cart()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var repository = new InMemoryCartRepository(cart);

        var act = () => new RemoveCartItemHandler(repository).HandleAsync(
            new RemoveCartItemCommand(cart.CustomerId, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repository.SaveCount.Should().Be(0);
    }

    private sealed class InMemoryCartRepository : ICartWriteRepository
    {
        private readonly Dictionary<Guid, Cart> carts;

        public InMemoryCartRepository(params Cart[] existingCarts)
        {
            carts = existingCarts.ToDictionary(cart => cart.CustomerId);
        }

        public int SaveCount { get; private set; }

        public Task<Cart?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(carts.GetValueOrDefault(customerId));

        public Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken cancellationToken)
        {
            if (!carts.TryGetValue(customerId, out var cart))
            {
                cart = Cart.Create(customerId);
                carts[customerId] = cart;
            }

            return Task.FromResult(cart);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
