using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class FulfillmentControllerTests(OrdersSqlServerFixture fixture)
{
    [Fact]
    public async Task Admin_can_filter_the_fulfillment_queue_and_read_order_details()
    {
        await fixture.ResetAsync();
        var order = await SeedConfirmedOrderAsync("Jane Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var queueResponse = await client.GetAsync("/api/v1/orders/fulfillment?status=Confirmed&search=Jane&pageSize=10");
        var queue = await queueResponse.Content.ReadFromJsonAsync<FulfillmentQueueDto>();
        var detailResponse = await client.GetAsync($"/api/v1/orders/fulfillment/{order.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<FulfillmentOrderDto>();

        queueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        queue!.TotalCount.Should().Be(1);
        queue.Items.Single().CustomerName.Should().Be("Jane Operator");
        queue.Items.Single().AvailableAction.Should().Be("StartPreparing");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.OrderId.Should().Be(order.Id);
        detail.AvailableAction.Should().Be("StartPreparing");
        detail.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Admin_gets_not_found_for_an_unknown_fulfillment_order()
    {
        await fixture.ResetAsync();
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/orders/fulfillment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_can_start_preparing_a_confirmed_order()
    {
        await fixture.ResetAsync();
        var order = await SeedConfirmedOrderAsync("Jane Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null);
        var command = await response.Content.ReadFromJsonAsync<FulfillmentCommandDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        command!.OrderId.Should().Be(order.Id);
        command.OrderStatus.Should().Be("Preparing");

        await using var db = fixture.CreateDbContext();
        (await db.Orders.FindAsync(order.Id))!.Status.Should().Be(OrderStatus.Preparing);
    }

    [Fact]
    public async Task Admin_gets_conflict_when_preparing_an_order_twice()
    {
        await fixture.ResetAsync();
        var order = await SeedConfirmedOrderAsync("Jane Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        (await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customer_cannot_read_the_operations_queue()
    {
        await fixture.ResetAsync();
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/v1/orders/fulfillment");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Order> SeedConfirmedOrderAsync(string customerName)
    {
        await using var db = fixture.CreateDbContext();
        var order = Order.Create(
            Guid.NewGuid(),
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

    private sealed record FulfillmentQueueDto(
        IReadOnlyCollection<FulfillmentSummaryDto> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record FulfillmentSummaryDto(
        Guid OrderId,
        Guid CustomerId,
        string CustomerName,
        int ItemCount,
        decimal Total,
        string PaymentStatus,
        string OrderStatus,
        DateTimeOffset CreatedAt,
        string AvailableAction);

    private sealed record FulfillmentOrderDto(
        Guid OrderId,
        Guid CustomerId,
        string CustomerName,
        string OrderStatus,
        string PaymentStatus,
        decimal Subtotal,
        decimal ShippingCost,
        decimal Total,
        string ShippingMethodCode,
        DateTimeOffset CreatedAt,
        object ShippingAddress,
        IReadOnlyCollection<object> Items,
        string AvailableAction);

    private sealed record FulfillmentCommandDto(Guid OrderId, string OrderStatus);
}
