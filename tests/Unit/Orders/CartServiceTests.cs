using FluentAssertions;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class CartServiceTests
{
    [Fact]
    public async Task Add_item_creates_cart_and_returns_cart_contents()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var repository = new InMemoryCartRepository();

        var result = await new CartService(repository).AddItemAsync(customerId, productId, 2, CancellationToken.None);

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

        await new CartService(repository).RemoveItemAsync(customerId, productId, CancellationToken.None);

        cart.Items.Should().BeEmpty();
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_cart()
    {
        var firstCustomerId = Guid.NewGuid();
        var secondCustomerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var firstCart = Cart.Create(firstCustomerId);
        firstCart.AddItem(productId, 1);
        var repository = new InMemoryCartRepository(firstCart, Cart.Create(secondCustomerId));

        var result = await new CartService(repository).GetAsync(secondCustomerId, CancellationToken.None);

        result.CustomerId.Should().Be(secondCustomerId);
        result.Items.Should().BeEmpty();
    }

    private sealed class InMemoryCartRepository : ICartRepository
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
