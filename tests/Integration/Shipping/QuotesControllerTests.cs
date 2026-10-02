extern alias ShippingApi;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Contracts.Authentication;
using Luna.IntegrationTests.Orders;
using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;
using Luna.Shipping.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Shipping;

[Collection(ShippingDatabaseCollection.Name)]
public sealed class QuotesControllerTests(ShippingSqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Anonymous_quote_request_is_rejected_and_writes_nothing()
    {
        await SeedStandardMethodAsync();
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/quotes", NewQuoteRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CountQuotesAsync()).Should().Be(0, "an unauthenticated caller must not be able to write to the Shipping database");
    }

    [Fact]
    public async Task Customer_token_cannot_create_a_quote()
    {
        await SeedStandardMethodAsync();
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync("/api/v1/quotes", NewQuoteRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a customer token authenticates but carries no orders client id or shipping scope");
        (await CountQuotesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Orders_service_token_creates_a_quote()
    {
        await SeedStandardMethodAsync();
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceHeader, LunaServiceClients.Orders);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceScopeHeader, LunaServiceScopes.ShippingShipmentsWrite);

        var orderId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/v1/quotes", NewQuoteRequest(orderId));
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        quote.Should().NotBeNull();
        quote!.OrderId.Should().Be(orderId);
        quote.ShippingMethodCode.Should().Be("STANDARD");
        quote.Cost.Should().Be(5.99m);
        (await CountQuotesAsync()).Should().Be(1);
    }

    private static QuoteShippingRequest NewQuoteRequest(Guid orderId) =>
        new(orderId, "STANDARD", "BR", "01310-100");

    private async Task SeedStandardMethodAsync()
    {
        await using var db = fixture.CreateDbContext();
        if (await db.ShippingMethods.AnyAsync(method => method.Code == "STANDARD"))
        {
            return;
        }

        db.ShippingMethods.Add(ShippingMethod.Create("STANDARD", "Standard delivery", 5.99m, 5));
        await db.SaveChangesAsync();
    }

    private async Task<int> CountQuotesAsync()
    {
        await using var db = fixture.CreateDbContext();
        return await db.ShippingQuotes.CountAsync();
    }

    private sealed record QuoteResponse(
        Guid QuoteId,
        Guid OrderId,
        string ShippingMethodCode,
        decimal Cost,
        int EstimatedDeliveryDays,
        DateTimeOffset CreatedAt);
}
