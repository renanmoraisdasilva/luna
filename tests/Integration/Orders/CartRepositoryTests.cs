using FluentAssertions;
using Luna.Orders.Infrastructure.Database;
using Luna.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [Fact]
    public async Task A_cart_created_between_the_existence_check_and_the_insert_is_returned()
    {
        await fixture.ResetAsync();
        var customerId = Guid.NewGuid();
        var competingCartId = Guid.NewGuid();
        var interceptor = new CompetingCartInterceptor(fixture, customerId, competingCartId);
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new OrdersDbContext(options);
        var repository = new CartRepository(db);

        var cart = await repository.GetOrCreateAsync(customerId, CancellationToken.None);

        cart.CustomerId.Should().Be(customerId);
        cart.Id.Should().Be(competingCartId);
        await using var verificationDb = fixture.CreateDbContext();
        (await verificationDb.Carts.CountAsync(value => value.CustomerId == customerId)).Should().Be(1);
    }

    private sealed class CompetingCartInterceptor(
        OrdersSqlServerFixture fixture,
        Guid customerId,
        Guid competingCartId) : SaveChangesInterceptor
    {
        private bool insertedCompetingCart;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (insertedCompetingCart)
            {
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            insertedCompetingCart = true;
            return InsertCompetingCartAsync(result, cancellationToken);
        }

        private async ValueTask<InterceptionResult<int>> InsertCompetingCartAsync(
            InterceptionResult<int> result,
            CancellationToken cancellationToken)
        {
            await using var competingDb = fixture.CreateDbContext();
            competingDb.Carts.Add(Luna.Orders.Domain.Cart.Create(customerId, competingCartId));
            await competingDb.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}
