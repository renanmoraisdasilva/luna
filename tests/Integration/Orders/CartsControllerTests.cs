using System.Security.Claims;
using FluentAssertions;
using Luna.Authentication;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class CartsControllerTests(OrdersSqlServerFixture fixture)
{
    [Fact]
    public async Task Rejects_requests_without_a_validated_customer()
    {
        await fixture.ResetAsync();
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/orders/cart");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returns_only_the_authenticated_customers_cart()
    {
        await fixture.ResetAsync();
        var firstCustomer = Guid.NewGuid();
        var secondCustomer = Guid.NewGuid();
        using var factory = new OrdersApiFactory(fixture);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        firstClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, firstCustomer.ToString());
        secondClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, secondCustomer.ToString());

        var addResponse = await firstClient.PostAsJsonAsync(
            "/api/v1/orders/cart/items",
            new { ProductId = Guid.NewGuid(), Quantity = 1 });
        var secondCartResponse = await secondClient.GetAsync("/api/v1/orders/cart");

        addResponse.IsSuccessStatusCode.Should().BeTrue();
        secondCartResponse.IsSuccessStatusCode.Should().BeTrue();
        var secondCart = await secondCartResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        secondCart!.CustomerId.Should().Be(secondCustomer);
        secondCart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_non_positive_item_quantity()
    {
        await fixture.ResetAsync();
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders/cart/items",
            new { ProductId = Guid.NewGuid(), Quantity = 0 });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rejects_changing_quantity_when_customer_has_no_cart()
    {
        await fixture.ResetAsync();
        using var factory = new OrdersApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.PutAsJsonAsync(
            $"/api/v1/orders/cart/items/{Guid.NewGuid()}",
            new { Quantity = 2 });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private sealed record CartResponseDto(Guid Id, Guid CustomerId, IReadOnlyCollection<CartItemResponseDto> Items);
    private sealed record CartItemResponseDto(Guid ProductId, int Quantity);
}

internal sealed class OrdersApiFactory(OrdersSqlServerFixture fixture) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = LunaAuthenticationDefaults.ValidationScheme;
                options.DefaultChallengeScheme = LunaAuthenticationDefaults.ValidationScheme;
                var scheme = options.Schemes.Single(item => item.Name == LunaAuthenticationDefaults.ValidationScheme);
                scheme.HandlerType = typeof(TestAuthenticationHandler);
            });
        });
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string CustomerHeader = "X-Test-Customer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(CustomerHeader, out var value)
            || !Guid.TryParse(value, out var customerId)
            || customerId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(LunaAuthentication.SubjectClaim, customerId.ToString()));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
