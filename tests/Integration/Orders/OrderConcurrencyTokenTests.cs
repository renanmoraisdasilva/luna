using FluentAssertions;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class OrderConcurrencyTokenTests : IAsyncLifetime
{
    private readonly OrdersSqlServerFixture fixture;

    public OrderConcurrencyTokenTests(OrdersSqlServerFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync()
    {
        fixture.ResetAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_a_write_that_is_based_on_a_stale_read()
    {
        var order = await SeedConfirmedOrderAsync();

        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();

        var first = await firstContext.Orders.FindAsync(order.Id);
        var second = await secondContext.Orders.FindAsync(order.Id);

        first!.RowVersion.Should().NotBeEmpty("the aggregate must carry a concurrency token");
        second!.RowVersion.Should().Equal(first.RowVersion);

        first.Prepare();
        await firstContext.SaveChangesAsync();

        var act = async () =>
        {
            second.Prepare();
            await secondContext.SaveChangesAsync();
        };

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Orders.FindAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public async Task Allows_a_sequential_write_from_a_freshly_loaded_aggregate()
    {
        var order = await SeedConfirmedOrderAsync();

        await using (var firstContext = fixture.CreateDbContext())
        {
            var loaded = await firstContext.Orders.FindAsync(order.Id);
            loaded!.Prepare();
            await firstContext.SaveChangesAsync();
        }

        await using var secondContext = fixture.CreateDbContext();
        var reloaded = await secondContext.Orders.FindAsync(order.Id);

        var act = async () =>
        {
            reloaded!.MarkShipped();
            await secondContext.SaveChangesAsync();
        };

        await act.Should().NotThrowAsync();

        await using var verify = fixture.CreateDbContext();
        var persisted = await verify.Orders.FindAsync(order.Id);
        persisted!.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task Issues_a_new_token_after_every_write()
    {
        var order = await SeedConfirmedOrderAsync();

        byte[] original;
        await using (var firstContext = fixture.CreateDbContext())
        {
            var loaded = await firstContext.Orders.FindAsync(order.Id);
            original = loaded!.RowVersion;
            loaded.Prepare();
            await firstContext.SaveChangesAsync();
        }

        await using var secondContext = fixture.CreateDbContext();
        var reloaded = await secondContext.Orders.FindAsync(order.Id);

        reloaded!.RowVersion.Should().NotEqual(
            original,
            "the token must change on every write, otherwise it cannot detect anything");
    }

    private async Task<Order> SeedConfirmedOrderAsync()
    {
        var order = Order.Create(
            Guid.NewGuid(),
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-CONCURRENCY", "Concurrency test product", 25, 1)],
            ShippingAddress.Create("Jane Customer", "1 Test Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            5,
            "concurrency-check",
            Guid.NewGuid(),
            Guid.NewGuid());
        order.Confirm();

        await using var db = fixture.CreateDbContext();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order;
    }
}
