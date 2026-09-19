using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Catalog.Contracts.Products;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Checkout;
using Luna.Orders.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class CheckoutWorkflowTests(OrdersSqlServerFixture fixture)
{
    [Fact]
    public async Task Completes_checkout_across_catalog_shipping_inventory_and_payments()
    {
        await fixture.ResetAsync();
        var state = new CheckoutServiceState();
        using var factory = new CheckoutApiFactory(fixture, state);
        using var client = CreateAuthenticatedClient(factory, state.CustomerId);

        await AddCartItemAsync(client, state.ProductId, 2);
        var response = await SubmitCheckoutAsync(client, "successful-checkout");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
        checkout.Should().NotBeNull();
        checkout!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        checkout.Total.Should().Be(29.99m);
        checkout.ReservationId.Should().Be(state.ReservationId);
        checkout.PaymentId.Should().Be(state.PaymentId);

        var orderResponse = await client.GetAsync($"/api/v1/orders/{checkout.OrderId}");
        orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponseDto>();
        order.Should().NotBeNull();
        order!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        order.Total.Should().Be(29.99m);
        order.Items.Should().ContainSingle(item =>
            item.ProductId == state.ProductId
            && item.UnitPrice == 12.50m
            && item.Quantity == 2
            && item.LineTotal == 25.00m);

        state.CatalogCalls.Should().Be(1);
        state.QuoteCalls.Should().Be(1);
        state.ReservationCalls.Should().Be(1);
        state.PaymentCalls.Should().Be(1);
        state.ReleaseCalls.Should().Be(0);
    }

    [Fact]
    public async Task Releases_inventory_when_payment_is_declined()
    {
        await fixture.ResetAsync();
        var state = new CheckoutServiceState { PaymentAuthorized = false };
        using var factory = new CheckoutApiFactory(fixture, state);
        using var client = CreateAuthenticatedClient(factory, state.CustomerId);

        await AddCartItemAsync(client, state.ProductId, 1);
        var response = await SubmitCheckoutAsync(client, "declined-payment");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.Code.Should().Be("PAYMENT_DECLINED");
        state.ReservationCalls.Should().Be(1);
        state.PaymentCalls.Should().Be(1);
        state.ReleaseCalls.Should().Be(1);

        await using var db = fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(item => item.CustomerId == state.CustomerId);
        order.Status.Should().Be(OrderStatus.PaymentFailed);
    }

    [Fact]
    public async Task Reports_failed_compensation_when_inventory_release_fails()
    {
        await fixture.ResetAsync();
        var state = new CheckoutServiceState
        {
            PaymentAuthorized = false,
            ReleaseSucceeds = false,
        };
        using var factory = new CheckoutApiFactory(fixture, state);
        using var client = CreateAuthenticatedClient(factory, state.CustomerId);

        await AddCartItemAsync(client, state.ProductId, 1);
        var response = await SubmitCheckoutAsync(client, "failed-compensation");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
        error!.Code.Should().Be("RESERVATION_RELEASE_FAILED");
        state.ReleaseCalls.Should().Be(1);

        await using var db = fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(item => item.CustomerId == state.CustomerId);
        order.Status.Should().Be(OrderStatus.PaymentFailed);
    }

    private static HttpClient CreateAuthenticatedClient(CheckoutApiFactory factory, Guid customerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, customerId.ToString());
        return client;
    }

    private static async Task AddCartItemAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/orders/cart/items",
            new { ProductId = productId, Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> SubmitCheckoutAsync(HttpClient client, string idempotencyKey)
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
                PaymentMethod = "test-card",
                Currency = "USD",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
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
}

internal sealed class CheckoutApiFactory(OrdersSqlServerFixture fixture, CheckoutServiceState state) : WebApplicationFactory<Program>
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

            services.RemoveAll<ICatalogCheckoutClient>();
            services.RemoveAll<IShippingCheckoutClient>();
            services.RemoveAll<IInventoryCheckoutClient>();
            services.RemoveAll<IPaymentsCheckoutClient>();
            services.AddSingleton<ICatalogCheckoutClient>(_ => new CatalogCheckoutClient(CreateClient(state)));
            services.AddSingleton<IShippingCheckoutClient>(_ => new ShippingCheckoutClient(CreateClient(state)));
            services.AddSingleton<IInventoryCheckoutClient>(_ => new InventoryCheckoutClient(CreateClient(state)));
            services.AddSingleton<IPaymentsCheckoutClient>(_ => new PaymentsCheckoutClient(CreateClient(state)));
        });
    }

    private static HttpClient CreateClient(CheckoutServiceState state) =>
        new(new CheckoutServiceHandler(state))
        {
            BaseAddress = new Uri("http://checkout-test/")
        };
}

internal sealed class CheckoutServiceState
{
    public Guid CustomerId { get; } = Guid.NewGuid();
    public Guid ProductId { get; } = Guid.NewGuid();
    public Guid ReservationId { get; } = Guid.NewGuid();
    public Guid PaymentId { get; } = Guid.NewGuid();
    public bool PaymentAuthorized { get; init; } = true;
    public bool ReleaseSucceeds { get; init; } = true;
    public int CatalogCalls { get; set; }
    public int QuoteCalls { get; set; }
    public int ReservationCalls { get; set; }
    public int PaymentCalls { get; set; }
    public int ReleaseCalls { get; set; }
}

internal sealed class CheckoutServiceHandler(CheckoutServiceState state) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/catalog/products/", StringComparison.Ordinal))
        {
            state.CatalogCalls++;
            return Task.FromResult(JsonResponse(new ProductResponse(
                state.ProductId,
                "LUNA-KEYBOARD",
                "Luna Keyboard",
                "A test product",
                12.50m,
                Guid.NewGuid(),
                "Electronics",
                "electronics",
                [])));
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/quotes")
        {
            state.QuoteCalls++;
            return Task.FromResult(JsonResponse(new
            {
                QuoteId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                ShippingMethodCode = "STANDARD",
                Cost = 4.99m,
                EstimatedDeliveryDays = 5,
                CreatedAt = DateTimeOffset.UtcNow,
            }));
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/reservations")
        {
            state.ReservationCalls++;
            return Task.FromResult(JsonResponse(new { ReservationId = state.ReservationId, OrderId = Guid.NewGuid(), Status = "Reserved" }));
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/release", StringComparison.Ordinal))
        {
            state.ReleaseCalls++;
            return Task.FromResult(state.ReleaseSucceeds ? JsonResponse(new { Status = "Released" }) : ErrorResponse());
        }

        if (request.Method == HttpMethod.Post && path == "/api/v1/payments/authorize")
        {
            state.PaymentCalls++;
            return Task.FromResult(JsonResponse(new
            {
                PaymentId = state.PaymentId,
                OrderId = Guid.NewGuid(),
                Amount = 29.99m,
                Currency = "USD",
                Status = state.PaymentAuthorized ? "Authorized" : "Declined",
                AttemptId = Guid.NewGuid(),
                Authorized = state.PaymentAuthorized,
                FailureCode = state.PaymentAuthorized ? null : "DECLINED",
                AttemptedAt = DateTimeOffset.UtcNow,
            }));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage JsonResponse(object value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static HttpResponseMessage ErrorResponse() =>
        new(HttpStatusCode.InternalServerError) { Content = JsonContent.Create(new { Message = "Downstream service failed." }) };
}