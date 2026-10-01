using FluentAssertions;
using Luna.Orders.Application.Carts;
using Luna.Orders.Contracts.Carts;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class GetCartHandlerTests
{
    [Fact]
    public async Task Returns_an_empty_non_persisted_cart_when_the_customer_has_no_cart()
    {
        var customerId = Guid.NewGuid();
        var repository = new InMemoryCartReadRepository(null);

        var result = await new GetCartHandler(repository).HandleAsync(customerId, CancellationToken.None);

        result.Id.Should().Be(Guid.Empty);
        result.CustomerId.Should().Be(customerId);
        result.Items.Should().BeEmpty();
    }

    private sealed class InMemoryCartReadRepository(CartResponse? cart) : ICartReadRepository
    {
        public Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(cart);
    }
}
