using FluentAssertions;
using Luna.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class CartRepositoryTests(OrdersSqlServerFixture fixture)
{
    [Fact]
    public async Task Concurrent_first_cart_creation_returns_the_same_persisted_cart()
    {
        await fixture.ResetAsync();
        var customerId = Guid.NewGuid();
        await using var firstDb = fixture.CreateDbContext();
        await using var secondDb = fixture.CreateDbContext();
        var firstRepository = new CartRepository(firstDb);
        var secondRepository = new CartRepository(secondDb);

        var results = await Task.WhenAll(
            firstRepository.GetOrCreateAsync(customerId, CancellationToken.None),
            secondRepository.GetOrCreateAsync(customerId, CancellationToken.None));

        results.Select(cart => cart.Id).Should().OnlyContain(id => id == results[0].Id);
        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Carts.CountAsync(cart => cart.CustomerId == customerId)).Should().Be(1);
    }

    [Fact]
    public async Task Existing_cart_is_returned_without_creating_a_duplicate()
    {
        await fixture.ResetAsync();
        var customerId = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();
        var repository = new CartRepository(db);
        var created = await repository.GetOrCreateAsync(customerId, CancellationToken.None);

        var loaded = await repository.GetOrCreateAsync(customerId, CancellationToken.None);

        loaded.Id.Should().Be(created.Id);
    }
}
