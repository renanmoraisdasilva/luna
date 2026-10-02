using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class FulfillmentQueueTests(OrdersSqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Queue_defaults_to_active_fulfillment_statuses_when_no_status_is_supplied()
    {
        var order = await SeedOrderAsync(OrderStatus.Confirmed, "Jane Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync("/api/v1/orders/fulfillment?pageSize=10");
        var queue = await response.Content.ReadFromJsonAsync<FulfillmentQueueDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        queue!.TotalCount.Should().Be(1);
        queue.Items.Single().OrderId.Should().Be(order.Id);
        queue.Items.Single().AvailableAction.Should().Be("StartPreparing");
        queue.Items.Single().PaymentStatus.Should().Be("Authorized");
    }

    [Fact]
    public async Task Queue_reports_the_action_for_a_preparing_order()
    {
        var order = await SeedOrderAsync(OrderStatus.Preparing, "Preparing Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync("/api/v1/orders/fulfillment?status=Preparing&pageSize=10");
        var queue = await response.Content.ReadFromJsonAsync<FulfillmentQueueDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        queue!.Items.Single().OrderId.Should().Be(order.Id);
        queue.Items.Single().OrderStatus.Should().Be(nameof(OrderStatus.Preparing));
        queue.Items.Single().AvailableAction.Should().Be("CreateShipment");
    }

    [Fact]
    public async Task Fulfillment_detail_reports_no_action_for_an_order_outside_the_workflow()
    {
        var order = await SeedOrderAsync(OrderStatus.Pending, "Pending Operator");
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/orders/fulfillment/{order.Id}");
        var detail = await response.Content.ReadFromJsonAsync<FulfillmentDetailDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.OrderId.Should().Be(order.Id);
        detail.OrderStatus.Should().Be(nameof(OrderStatus.Pending));
        detail.AvailableAction.Should().Be("None");
    }

    [Theory]
    [InlineData("status=Bogus")]
    [InlineData("status=Pending")]
    [InlineData("status=Shipped")]
    public async Task Queue_rejects_statuses_that_are_not_fulfillment_work(string query)
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/orders/fulfillment?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("INVALID_REQUEST");
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Queue_rejects_invalid_paging(string query)
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/orders/fulfillment?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task Fulfillment_commands_return_not_found_for_unknown_ids()
    {
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");
        var unknownOrderId = Guid.NewGuid();
        var unknownShipmentId = Guid.NewGuid();

        var endpoints = new[]
        {
            $"/api/v1/orders/fulfillment/{unknownOrderId}/prepare",
            $"/api/v1/orders/fulfillment/{unknownOrderId}/shipment",
            $"/api/v1/orders/fulfillment/shipments/{unknownShipmentId}/in-transit",
            $"/api/v1/orders/fulfillment/shipments/{unknownShipmentId}/delivered",
        };

        foreach (var endpoint in endpoints)
        {
            var response = await client.PostAsync(endpoint, content: null);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"POST {endpoint} should report a missing resource");
        }
    }

    private async Task<Order> SeedOrderAsync(OrderStatus status, string customerName)
    {
        await using var db = fixture.CreateDbContext();
        var order = Order.Create(
            Guid.NewGuid(),
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-001", "Test product", 25, 1)],
            ShippingAddress.Create(customerName, "1 Test Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            5,
            Guid.NewGuid().ToString());

        if (status is not OrderStatus.Pending)
        {
            order.Confirm();
            order.RecordCheckoutResult(Guid.NewGuid(), Guid.NewGuid());
        }

        if (status is OrderStatus.Preparing)
        {
            order.Prepare();
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private sealed record ErrorDto(string Code, string Message);

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

    private sealed record FulfillmentDetailDto(
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
}
