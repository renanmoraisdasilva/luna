using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Orders.Application.Orders;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Checkout;
using Luna.Orders.Infrastructure.Database;
using Luna.IntegrationTests.Shipping;
using Luna.Shipping.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CommerceWorkflowCollection :
    ICollectionFixture<OrdersSqlServerFixture>,
    ICollectionFixture<ShippingSqlServerFixture>
{
    public const string Name = "Orders and Shipping workflow";
}

[Collection(CommerceWorkflowCollection.Name)]
public sealed class FulfillmentShippingWorkflowTests(
    OrdersSqlServerFixture ordersFixture,
    ShippingSqlServerFixture shippingFixture) : IAsyncLifetime
{
    /// <summary>
    /// Both databases are reset here rather than as the first statement of each test, so a test that
    /// throws during setup cannot leak rows into the next one.
    /// </summary>
    public async Task InitializeAsync()
    {
        await ordersFixture.ResetAsync();
        await shippingFixture.ResetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Completes_fulfillment_and_shipping_lifecycle_across_real_http_boundaries()
    {
        var order = await SeedConfirmedOrderAsync();
        using var shippingFactory = new ShippingApiFactory(shippingFixture);
        using var ordersFactory = new OrdersWithShippingFactory(ordersFixture, shippingFactory);
        using var client = ordersFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var prepareResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null);
        prepareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/shipment", content: null);
        var created = await createResponse.Content.ReadFromJsonAsync<FulfillmentCommandResponse>();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        created!.OrderStatus.Should().Be(nameof(OrderStatus.Shipped));

        await using var ordersDb = ordersFixture.CreateDbContext();
        var persistedOrder = await ordersDb.Orders.FindAsync(order.Id);
        persistedOrder!.ShipmentId.Should().NotBeNull();
        persistedOrder.Status.Should().Be(OrderStatus.Shipped);

        Guid shipmentId;
        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var shipment = await shippingDb.Shipments.FindAsync(persistedOrder.ShipmentId!.Value);
            shipment.Should().NotBeNull();
            shipment!.Status.Should().Be(ShipmentStatus.Created);
            shipment.TrackingNumber.Should().NotBeNullOrWhiteSpace();
            shipmentId = shipment.Id;
        }

        var inTransitResponse = await client.PostAsync($"/api/v1/orders/fulfillment/shipments/{shipmentId}/in-transit", content: null);
        inTransitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deliveredResponse = await client.PostAsync($"/api/v1/orders/fulfillment/shipments/{shipmentId}/delivered", content: null);
        var delivered = await deliveredResponse.Content.ReadFromJsonAsync<FulfillmentCommandResponse>();
        deliveredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        delivered!.OrderStatus.Should().Be(nameof(OrderStatus.Delivered));

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var shipment = await shippingDb.Shipments
                .Include(item => item.TrackingEvents)
                .SingleAsync(item => item.Id == shipmentId);
            shipment.Status.Should().Be(ShipmentStatus.Delivered);
            shipment.TrackingEvents
                .OrderBy(item => item.Status switch
                {
                    nameof(ShipmentStatus.Created) => 0,
                    nameof(ShipmentStatus.InTransit) => 1,
                    nameof(ShipmentStatus.Delivered) => 2,
                    _ => 3,
                })
                .Select(item => item.Status)
                .Should().Equal(nameof(ShipmentStatus.Created), nameof(ShipmentStatus.InTransit), nameof(ShipmentStatus.Delivered));
        }

        await ordersDb.Entry(persistedOrder).ReloadAsync();
        persistedOrder.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public async Task Rejects_invalid_and_duplicate_shipment_transitions_across_real_http_boundaries()
    {
        var order = await SeedConfirmedOrderAsync();
        using var shippingFactory = new ShippingApiFactory(shippingFixture);
        using var ordersFactory = new OrdersWithShippingFactory(ordersFixture, shippingFactory);
        using var client = ordersFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var notPreparingResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/shipment", content: null);
        notPreparingResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var createResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/shipment", content: null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var ordersDb = ordersFixture.CreateDbContext();
        var persistedOrder = await ordersDb.Orders.FindAsync(order.Id);
        var shipmentId = persistedOrder!.ShipmentId!.Value;

        var directDeliveryResponse = await client.PostAsync($"/api/v1/orders/fulfillment/shipments/{shipmentId}/delivered", content: null);
        directDeliveryResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var duplicateResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/shipment", content: null);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var shippingDb = shippingFixture.CreateDbContext();
        var shipment = await shippingDb.Shipments.FindAsync(shipmentId);
        shipment!.Status.Should().Be(ShipmentStatus.Created);
        persistedOrder.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task Moving_a_shipment_in_transit_advances_the_shipment_but_not_the_order()
    {
        // The in-transit transition belongs to Shipping, not to Orders. OrderStatus has no InTransit member:
        // the order moves Pending -> Confirmed -> Preparing -> Shipped -> Delivered, so an order that has been
        // shipped stays Shipped while its shipment is in transit.
        //
        // MarkShipmentInTransitHandler therefore calls a guard (EnsureStatusForShipmentInTransit, which only
        // asserts the order is Shipped) and makes no change to the aggregate, so it correctly has no
        // SaveChangesAsync. Its sibling MarkShipmentDeliveredHandler does mutate the order and does save.
        // This test pins that asymmetry down so a later reader does not "fix" it into an invented order
        // transition that the domain does not model.
        var order = await SeedConfirmedOrderAsync();
        using var shippingFactory = new ShippingApiFactory(shippingFixture);
        using var ordersFactory = new OrdersWithShippingFactory(ordersFixture, shippingFactory);
        using var client = ordersFactory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/prepare", content: null);
        var createResponse = await client.PostAsync($"/api/v1/orders/fulfillment/{order.Id}/shipment", content: null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Guid shipmentId;
        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var persistedOrder = await ordersDb.Orders.FindAsync(order.Id);
            shipmentId = persistedOrder!.ShipmentId!.Value;
        }

        var inTransitResponse = await client.PostAsync(
            $"/api/v1/orders/fulfillment/shipments/{shipmentId}/in-transit",
            content: null);
        inTransitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // The response reports the order status, which must still be Shipped.
        var inTransit = await inTransitResponse.Content.ReadFromJsonAsync<FulfillmentCommandResponse>();
        inTransit!.OrderStatus.Should().Be(nameof(OrderStatus.Shipped));

        // The order row is unchanged: no transition, and nothing was written to it.
        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var reloadedOrder = await ordersDb.Orders.FindAsync(order.Id);
            reloadedOrder!.Status.Should().Be(OrderStatus.Shipped);
        }

        // Shipping owns the in-transit transition, and that is where it is persisted.
        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var shipment = await shippingDb.Shipments.FindAsync(shipmentId);
            shipment!.Status.Should().Be(ShipmentStatus.InTransit);
        }

        // A second in-transit attempt is refused by the Shipping aggregate, which requires Created. This is
        // what stops two concurrent operator clicks from double-advancing the shipment, so Orders does not
        // need its own concurrency token for this transition.
        var repeatedResponse = await client.PostAsync(
            $"/api/v1/orders/fulfillment/shipments/{shipmentId}/in-transit",
            content: null);
        repeatedResponse.IsSuccessStatusCode.Should().BeFalse(
            "a shipment already in transit cannot be marked in transit again");

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var shipment = await shippingDb.Shipments.FindAsync(shipmentId);
            shipment!.Status.Should().Be(ShipmentStatus.InTransit);
        }
    }

    private async Task<Order> SeedConfirmedOrderAsync()
    {
        var orderId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var order = Order.Create(
            customerId,
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-001", "Test product", 25, 1)],
            ShippingAddress.Create("Jane Customer", "1 Test Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            5,
            "integration-checkout",
            orderId,
            quoteId);
        order.Confirm();
        order.RecordCheckoutResult(Guid.NewGuid(), Guid.NewGuid());

        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            ordersDb.Orders.Add(order);
            await ordersDb.SaveChangesAsync();
        }

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            shippingDb.ShippingQuotes.Add(ShippingQuote.Create(orderId, "STANDARD", 5, 3, "US", "78701", quoteId));
            await shippingDb.SaveChangesAsync();
        }

        return order;
    }

    private sealed record FulfillmentCommandResponse(Guid OrderId, string OrderStatus);
}

internal sealed class OrdersWithShippingFactory(
    OrdersSqlServerFixture fixture,
    ShippingApiFactory shippingFactory) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = Luna.Authentication.LunaAuthenticationDefaults.ValidationScheme;
                options.DefaultChallengeScheme = Luna.Authentication.LunaAuthenticationDefaults.ValidationScheme;
                var scheme = options.Schemes.Single(item => item.Name == Luna.Authentication.LunaAuthenticationDefaults.ValidationScheme);
                scheme.HandlerType = typeof(TestAuthenticationHandler);
            });

            services.RemoveAll<IShippingFulfillmentClient>();
            services.AddSingleton<IShippingFulfillmentClient>(_ =>
            {
                var client = shippingFactory.CreateClient();
                client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceHeader, "orders");
                return new ShippingFulfillmentClient(client);
            });
        });
    }
}
