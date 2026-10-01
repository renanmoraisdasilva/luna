using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Orders.Contracts.Orders;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class OrdersControllerTests(OrdersSqlServerFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The database is reset as a lifecycle hook rather than as the first statement of each test, so a
    /// test that throws during setup cannot leak rows into the next one.
    /// </summary>
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Health_endpoint_responds()
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Customer_reads_only_their_own_orders()
    {
        var customerId = Guid.NewGuid();
        var order = await SeedOrderAsync(customerId, "Jane Customer");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, customerId.ToString());

        var listResponse = await client.GetAsync("/api/v1/orders");
        var orders = await listResponse.Content.ReadFromJsonAsync<OrderSummaryResponse[]>();
        var detailResponse = await client.GetAsync($"/api/v1/orders/{order.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<OrderResponse>();

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        orders!.Should().ContainSingle();
        orders![0].Id.Should().Be(order.Id);
        orders[0].Status.Should().Be(nameof(OrderStatus.Confirmed));
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.Id.Should().Be(order.Id);
        detail.Total.Should().Be(order.Total);
    }

    [Fact]
    public async Task Customer_gets_not_found_for_an_unknown_order()
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/orders/{orderId}")]
    public async Task Requests_without_a_customer_subject_are_rejected_with_a_conflict(string template)
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync(template.Replace("{orderId}", Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("FULFILLMENT_CONFLICT");
    }

    private async Task<Order> SeedOrderAsync(Guid customerId, string customerName)
    {
        await using var db = fixture.CreateDbContext();
        var order = Order.Create(
            customerId,
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-001", "Test product", 25, 1)],
            ShippingAddress.Create(customerName, "1 Test Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            5,
            Guid.NewGuid().ToString());
        order.Confirm();
        order.RecordCheckoutResult(Guid.NewGuid(), Guid.NewGuid());
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private sealed record ErrorDto(string Code, string Message);
}
