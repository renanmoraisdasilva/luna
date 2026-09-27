extern alias CatalogApi;
extern alias InventoryApi;
extern alias PaymentsApi;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Authentication;
using Luna.Catalog.Domain;
using Luna.Contracts.Authentication;
using Luna.IntegrationTests.Catalog;
using Luna.IntegrationTests.Inventory;
using Luna.IntegrationTests.Payments;
using Luna.IntegrationTests.Shipping;
using Luna.Inventory.Domain;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Application.Orders;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Checkout;
using Luna.Payments.Domain;
using Luna.Shipping.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Luna.IntegrationTests.Orders;

/// <summary>
/// Phase 1 SPEC-TEST-003: proves checkout and its failure matrix across real service
/// boundaries. Every downstream service runs as its own host with its own database;
/// only the Orders host under test is wired to them through real HTTP calls.
/// </summary>
[Collection(CheckoutCommerceCollection.Name)]
public sealed class CheckoutCrossServiceWorkflowTests(
    OrdersSqlServerFixture ordersFixture,
    CatalogSqlServerFixture catalogFixture,
    InventorySqlServerFixture inventoryFixture,
    PaymentsSqlServerFixture paymentsFixture,
    ShippingSqlServerFixture shippingFixture)
{
    private const decimal UnitPrice = 12.50m;

    [Fact]
    public async Task Completes_checkout_across_real_catalog_shipping_inventory_and_payments_services()
    {
        await ResetAsync();
        using var stack = CreateStack();
        var customerId = Guid.NewGuid();
        var productId = await SeedProductAsync(quantityOnHand: 5);
        var client = CreateCustomerClient(stack, customerId);

        await AddCartItemAsync(client, productId, quantity: 2);
        var response = await SubmitCheckoutAsync(client, "cross-service-success");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        checkout.Should().NotBeNull();
        checkout!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        checkout.ReservationId.Should().NotBeEmpty();
        checkout.PaymentId.Should().NotBeEmpty();

        ShippingQuote quote;
        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            quote = await shippingDb.ShippingQuotes.SingleAsync(item => item.OrderId == checkout.OrderId);
        }

        quote.ShippingMethodCode.Should().Be("STANDARD");
        quote.Cost.Should().BePositive();
        checkout.Total.Should().Be(2 * UnitPrice + quote.Cost);

        var orderResponse = await client.GetAsync($"/api/v1/orders/{checkout.OrderId}");
        orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponseDto>();
        order.Should().NotBeNull();
        order!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        order.ShippingMethodCode.Should().Be("STANDARD");
        order.ShippingCost.Should().Be(quote.Cost);
        order.Total.Should().Be(checkout.Total);
        order.Items.Should().ContainSingle(item =>
            item.ProductId == productId
            && item.UnitPrice == UnitPrice
            && item.Quantity == 2
            && item.LineTotal == 2 * UnitPrice);

        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var persisted = await ordersDb.Orders.SingleAsync(item => item.Id == checkout.OrderId);
            persisted.Status.Should().Be(OrderStatus.Confirmed);
            persisted.ShippingQuoteId.Should().Be(quote.Id);
            persisted.ShippingCost.Should().Be(quote.Cost);
            persisted.InventoryReservationId.Should().Be(checkout.ReservationId);
            persisted.PaymentId.Should().Be(checkout.PaymentId);
        }

        await using (var inventoryDb = inventoryFixture.CreateDbContext())
        {
            var reservation = await inventoryDb.InventoryReservations
                .SingleAsync(item => item.OrderId == checkout.OrderId);
            reservation.Id.Should().Be(checkout.ReservationId);
            reservation.Status.Should().Be(InventoryReservationStatus.Active);

            var stock = await inventoryDb.Stocks.SingleAsync(item => item.ProductId == productId);
            stock.QuantityReserved.Should().Be(2);
            stock.AvailableQuantity.Should().Be(3);
        }

        await using (var paymentsDb = paymentsFixture.CreateDbContext())
        {
            var payment = await paymentsDb.Payments.SingleAsync(item => item.OrderId == checkout.OrderId);
            payment.Id.Should().Be(checkout.PaymentId);
            payment.Status.Should().Be(PaymentStatus.Authorized);
            payment.Amount.Should().Be(checkout.Total);
            payment.Currency.Should().Be("USD");
            (await paymentsDb.PaymentAttempts.CountAsync(item => item.PaymentId == payment.Id && item.Succeeded))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task Rejects_checkout_when_inventory_is_insufficient_without_attempting_payment()
    {
        await ResetAsync();
        using var stack = CreateStack();
        var customerId = Guid.NewGuid();
        var productId = await SeedProductAsync(quantityOnHand: 1);
        var client = CreateCustomerClient(stack, customerId);

        await AddCartItemAsync(client, productId, quantity: 2);
        var response = await SubmitCheckoutAsync(client, "cross-service-insufficient-inventory");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.Code.Should().Be("INVENTORY_UNAVAILABLE");

        Guid orderId;
        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var order = await ordersDb.Orders.SingleAsync(item => item.CustomerId == customerId);
            orderId = order.Id;
            order.Status.Should().Be(OrderStatus.Cancelled);
        }

        await using (var inventoryDb = inventoryFixture.CreateDbContext())
        {
            (await inventoryDb.InventoryReservations.CountAsync(item => item.OrderId == orderId))
                .Should().Be(0);

            var stock = await inventoryDb.Stocks.SingleAsync(item => item.ProductId == productId);
            stock.QuantityReserved.Should().Be(0);
            stock.AvailableQuantity.Should().Be(1);
        }

        await using (var paymentsDb = paymentsFixture.CreateDbContext())
        {
            (await paymentsDb.Payments.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task Releases_inventory_and_fails_the_order_when_payment_is_declined()
    {
        await ResetAsync();
        using var stack = CreateStack();
        var customerId = Guid.NewGuid();
        var productId = await SeedProductAsync(quantityOnHand: 5);
        var client = CreateCustomerClient(stack, customerId);

        await AddCartItemAsync(client, productId, quantity: 1);
        var response = await SubmitCheckoutAsync(
            client,
            "cross-service-declined-payment",
            paymentMethod: "declined");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.Code.Should().Be("PAYMENT_DECLINED");

        Guid orderId;
        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var order = await ordersDb.Orders.SingleAsync(item => item.CustomerId == customerId);
            orderId = order.Id;
            order.Status.Should().Be(OrderStatus.PaymentFailed);
        }

        await using (var inventoryDb = inventoryFixture.CreateDbContext())
        {
            var reservation = await inventoryDb.InventoryReservations
                .SingleAsync(item => item.OrderId == orderId);
            reservation.Status.Should().Be(InventoryReservationStatus.Released);

            var stock = await inventoryDb.Stocks.SingleAsync(item => item.ProductId == productId);
            stock.QuantityReserved.Should().Be(0);
            stock.AvailableQuantity.Should().Be(5);
        }

        await using (var paymentsDb = paymentsFixture.CreateDbContext())
        {
            var payment = await paymentsDb.Payments.SingleAsync(item => item.OrderId == orderId);
            payment.Status.Should().Be(PaymentStatus.Failed);
            (await paymentsDb.PaymentAttempts
                .CountAsync(item => item.PaymentId == payment.Id && item.FailureCode == "PAYMENT_DECLINED"))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task Keeps_order_preparing_when_shipping_rejects_shipment_creation()
    {
        await ResetAsync();
        using var stack = CreateStack();
        var customerId = Guid.NewGuid();
        var productId = await SeedProductAsync(quantityOnHand: 5);
        var client = CreateCustomerClient(stack, customerId);

        await AddCartItemAsync(client, productId, quantity: 1);
        var response = await SubmitCheckoutAsync(client, "cross-service-shipment-failure");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        checkout.Should().NotBeNull();
        var orderId = checkout!.OrderId;

        // Make Shipping reject shipment creation by removing the quote Orders snapshots.
        ShippingQuote removedQuote;
        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var quote = await shippingDb.ShippingQuotes.SingleAsync(item => item.OrderId == orderId);
            removedQuote = ShippingQuote.Create(
                quote.OrderId,
                quote.ShippingMethodCode,
                quote.Cost,
                quote.EstimatedDeliveryDays,
                quote.Country,
                quote.PostalCode,
                quote.Id,
                quote.CreatedAt);
            shippingDb.ShippingQuotes.Remove(quote);
            await shippingDb.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient(stack);
        var prepareResponse = await adminClient.PostAsync(
            $"/api/v1/orders/fulfillment/{orderId}/prepare",
            content: null);
        prepareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var failedShipmentResponse = await adminClient.PostAsync(
            $"/api/v1/orders/fulfillment/{orderId}/shipment",
            content: null);
        failedShipmentResponse.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var persisted = await ordersDb.Orders.SingleAsync(item => item.Id == orderId);
            persisted.Status.Should().Be(OrderStatus.Preparing);
            persisted.ShipmentId.Should().BeNull();
        }

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            (await shippingDb.Shipments.AnyAsync(item => item.OrderId == orderId)).Should().BeFalse();
        }

        // Restore the quote and run Create Shipment again through the Operations workflow.
        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            shippingDb.ShippingQuotes.Add(ShippingQuote.Create(
                removedQuote.OrderId,
                removedQuote.ShippingMethodCode,
                removedQuote.Cost,
                removedQuote.EstimatedDeliveryDays,
                removedQuote.Country,
                removedQuote.PostalCode,
                removedQuote.Id,
                removedQuote.CreatedAt));
            await shippingDb.SaveChangesAsync();
        }

        var retryResponse = await adminClient.PostAsync(
            $"/api/v1/orders/fulfillment/{orderId}/shipment",
            content: null);
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retry = await retryResponse.Content.ReadFromJsonAsync<FulfillmentCommandResponse>();
        retry!.OrderStatus.Should().Be(nameof(OrderStatus.Shipped));

        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            var persisted = await ordersDb.Orders.SingleAsync(item => item.Id == orderId);
            persisted.Status.Should().Be(OrderStatus.Shipped);
            persisted.ShipmentId.Should().NotBeNull();
        }

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            var shipment = await shippingDb.Shipments.SingleAsync(item => item.OrderId == orderId);
            shipment.Status.Should().Be(ShipmentStatus.Created);
            shipment.TrackingNumber.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Replays_repeated_checkout_requests_with_the_same_idempotency_key()
    {
        await ResetAsync();
        using var stack = CreateStack();
        var customerId = Guid.NewGuid();
        var productId = await SeedProductAsync(quantityOnHand: 5);
        var client = CreateCustomerClient(stack, customerId);

        await AddCartItemAsync(client, productId, quantity: 2);
        var firstResponse = await SubmitCheckoutAsync(client, "cross-service-replay");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await firstResponse.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        first.Should().NotBeNull();

        var secondResponse = await SubmitCheckoutAsync(client, "cross-service-replay");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await secondResponse.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        second.Should().NotBeNull();

        second!.OrderId.Should().Be(first!.OrderId);
        second.Status.Should().Be(nameof(OrderStatus.Confirmed));
        second.Total.Should().Be(first.Total);
        second.ReservationId.Should().Be(first.ReservationId);
        second.PaymentId.Should().Be(first.PaymentId);

        await using (var ordersDb = ordersFixture.CreateDbContext())
        {
            (await ordersDb.Orders.CountAsync(item => item.CustomerId == customerId)).Should().Be(1);
        }

        await using (var inventoryDb = inventoryFixture.CreateDbContext())
        {
            (await inventoryDb.InventoryReservations.CountAsync(item => item.OrderId == first.OrderId))
                .Should().Be(1);

            var stock = await inventoryDb.Stocks.SingleAsync(item => item.ProductId == productId);
            stock.QuantityReserved.Should().Be(2);
        }

        await using (var paymentsDb = paymentsFixture.CreateDbContext())
        {
            (await paymentsDb.Payments.CountAsync(item => item.OrderId == first.OrderId)).Should().Be(1);
        }

        await using (var shippingDb = shippingFixture.CreateDbContext())
        {
            (await shippingDb.ShippingQuotes.CountAsync(item => item.OrderId == first.OrderId)).Should().Be(1);
        }
    }

    private async Task ResetAsync()
    {
        await ordersFixture.ResetAsync();
        await catalogFixture.ResetAsync();
        await inventoryFixture.ResetAsync();
        await paymentsFixture.ResetAsync();
        await shippingFixture.ResetAsync();
    }

    private CommerceStack CreateStack()
    {
        var catalog = new CatalogApiFactory(catalogFixture);
        var inventory = new InventoryApiFactory(inventoryFixture);
        var payments = new PaymentsApiFactory(paymentsFixture);
        var shipping = new ShippingApiFactory(shippingFixture);

        var orders = new CheckoutCrossServiceOrdersFactory(
            ordersFixture,
            catalog.CreateClient(),
            CreateServiceClient(shipping, LunaServiceScopes.ShippingShipmentsWrite),
            CreateServiceClient(inventory, LunaServiceScopes.InventoryReservationsWrite),
            CreateServiceClient(payments, LunaServiceScopes.PaymentsAuthorize));

        return new CommerceStack(orders, catalog, inventory, payments, shipping);
    }

    private async Task<Guid> SeedProductAsync(int quantityOnHand)
    {
        var productId = Guid.NewGuid();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = $"Checkout Category {productId:N}",
            Slug = $"checkout-category-{productId:N}",
        };
        var product = new Product
        {
            Id = productId,
            CategoryId = category.Id,
            Category = category,
            Sku = $"SKU-{productId:N}",
            Name = "Luna Checkout Keyboard",
            Description = "A keyboard used by the cross-service checkout workflow.",
            CurrentPrice = UnitPrice,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await using (var catalogDb = catalogFixture.CreateDbContext())
        {
            catalogDb.AddRange(category, product);
            await catalogDb.SaveChangesAsync();
        }

        await using (var inventoryDb = inventoryFixture.CreateDbContext())
        {
            inventoryDb.Stocks.Add(Stock.Create(productId, quantityOnHand));
            await inventoryDb.SaveChangesAsync();
        }

        return productId;
    }

    private static HttpClient CreateCustomerClient(CommerceStack stack, Guid customerId)
    {
        var client = stack.Orders.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, customerId.ToString());
        return client;
    }

    private static HttpClient CreateAdminClient(CommerceStack stack)
    {
        var client = stack.Orders.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");
        return client;
    }

    private static HttpClient CreateServiceClient<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory,
        string scope)
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceHeader, LunaServiceClients.Orders);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceScopeHeader, scope);
        return client;
    }

    private static async Task AddCartItemAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/orders/cart/items",
            new { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> SubmitCheckoutAsync(
        HttpClient client,
        string idempotencyKey,
        string paymentMethod = "test-card")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(new
            {
                FullName = "Jane Doe",
                AddressLine1 = "123 Luna Street",
                AddressLine2 = (string?)null,
                City = "Austin",
                StateOrProvince = "Texas",
                PostalCode = "78701",
                Country = "US",
                ShippingMethodCode = "STANDARD",
                PaymentMethod = paymentMethod,
                Currency = "USD",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private sealed class CommerceStack(
        CheckoutCrossServiceOrdersFactory orders,
        CatalogApiFactory catalog,
        InventoryApiFactory inventory,
        PaymentsApiFactory payments,
        ShippingApiFactory shipping) : IDisposable
    {
        public CheckoutCrossServiceOrdersFactory Orders { get; } = orders;

        public void Dispose()
        {
            Orders.Dispose();
            shipping.Dispose();
            inventory.Dispose();
            payments.Dispose();
            catalog.Dispose();
        }
    }

    private sealed record ApiErrorDto(string Code, string Message);

    private sealed record CheckoutResponseDto(
        Guid OrderId,
        string Status,
        decimal Total,
        string Currency,
        Guid ReservationId,
        Guid PaymentId);

    private sealed record OrderResponseDto(
        Guid Id,
        Guid CustomerId,
        string Status,
        decimal Subtotal,
        decimal ShippingCost,
        decimal Total,
        string ShippingMethodCode,
        DateTimeOffset CreatedAt,
        object ShippingAddress,
        IReadOnlyCollection<OrderItemResponseDto> Items);

    private sealed record OrderItemResponseDto(
        Guid ProductId,
        string Sku,
        string ProductName,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);

    private sealed record FulfillmentCommandResponse(Guid OrderId, string OrderStatus);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CheckoutCommerceCollection :
    ICollectionFixture<OrdersSqlServerFixture>,
    ICollectionFixture<CatalogSqlServerFixture>,
    ICollectionFixture<InventorySqlServerFixture>,
    ICollectionFixture<PaymentsSqlServerFixture>,
    ICollectionFixture<ShippingSqlServerFixture>
{
    public const string Name = "Checkout across services";
}

internal sealed class CheckoutCrossServiceOrdersFactory(
    OrdersSqlServerFixture fixture,
    HttpClient catalogClient,
    HttpClient shippingClient,
    HttpClient inventoryClient,
    HttpClient paymentsClient) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
        builder.UseSetting("CatalogApi", "http://catalog.test/");
        builder.UseSetting("ShippingApi", "http://shipping.test/");
        builder.UseSetting("InventoryApi", "http://inventory.test/");
        builder.UseSetting("PaymentsApi", "http://payments.test/");
        builder.ConfigureTestServices(services =>
        {
            CrossServiceTestAuth.UseTestAuthentication(services);

            services.RemoveAll<ICatalogCheckoutClient>();
            services.RemoveAll<IShippingCheckoutClient>();
            services.RemoveAll<IInventoryCheckoutClient>();
            services.RemoveAll<IPaymentsCheckoutClient>();
            services.RemoveAll<IShippingFulfillmentClient>();

            services.AddSingleton<ICatalogCheckoutClient>(_ => new CatalogCheckoutClient(catalogClient));
            services.AddSingleton<IShippingCheckoutClient>(_ => new ShippingCheckoutClient(shippingClient));
            services.AddSingleton<IInventoryCheckoutClient>(_ => new InventoryCheckoutClient(inventoryClient));
            services.AddSingleton<IPaymentsCheckoutClient>(_ => new PaymentsCheckoutClient(paymentsClient));
            services.AddSingleton<IShippingFulfillmentClient>(_ => new ShippingFulfillmentClient(shippingClient));
        });
    }
}

internal sealed class CatalogApiFactory(CatalogSqlServerFixture fixture)
    : WebApplicationFactory<CatalogApi.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
}

internal sealed class InventoryApiFactory(InventorySqlServerFixture fixture)
    : WebApplicationFactory<InventoryApi.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
        builder.ConfigureTestServices(CrossServiceTestAuth.UseTestAuthentication);
    }
}

internal sealed class PaymentsApiFactory(PaymentsSqlServerFixture fixture)
    : WebApplicationFactory<PaymentsApi.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
        builder.ConfigureTestServices(CrossServiceTestAuth.UseTestAuthentication);
    }
}

internal static class CrossServiceTestAuth
{
    public static void UseTestAuthentication(IServiceCollection services) =>
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = LunaAuthenticationDefaults.ValidationScheme;
            options.DefaultChallengeScheme = LunaAuthenticationDefaults.ValidationScheme;
            var scheme = options.Schemes.Single(item => item.Name == LunaAuthenticationDefaults.ValidationScheme);
            scheme.HandlerType = typeof(TestAuthenticationHandler);
        });
}
