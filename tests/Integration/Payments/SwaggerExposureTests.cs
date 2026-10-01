extern alias PaymentsApi;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Luna.IntegrationTests.Payments;

/// <summary>
/// Tests that the OpenAPI document and its UI are development-only.
///
/// Payments is the service to check: it is internal-only, every mutating endpoint requires a service token
/// with the payments.authorize scope, and its document names those scopes. Publishing it would tell an
/// anonymous caller exactly which endpoints exist and which scopes they need, which is the reconnaissance
/// half of the attack the service-scope policies are there to prevent.
/// </summary>
public sealed class SwaggerExposureTests : IClassFixture<PaymentsSqlServerFixture>
{
    private readonly PaymentsSqlServerFixture fixture;

    public SwaggerExposureTests(PaymentsSqlServerFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Does_not_serve_the_openapi_document_in_production()
    {
        using var factory = new PaymentsProductionFactory(fixture);

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(
            System.Net.HttpStatusCode.NotFound,
            "the OpenAPI document must not be reachable in production");

        (await response.Content.ReadAsStringAsync()).Should().NotContain("payments.authorize");
    }

    [Fact]
    public async Task Does_not_serve_the_openapi_ui_in_production()
    {
        using var factory = new PaymentsProductionFactory(fixture);

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Still_serves_the_openapi_document_in_development()
    {
        // The gate must not accidentally disable local development, where the document is how the API is
        // explored. This is the half that would silently regress if the condition were inverted.
        using var factory = new PaymentsDevelopmentFactory(fixture);

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("/api/v1/payments/authorize");
    }
}

internal sealed class PaymentsProductionFactory(PaymentsSqlServerFixture fixture)
    : WebApplicationFactory<PaymentsApi::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
    }
}

internal sealed class PaymentsDevelopmentFactory(PaymentsSqlServerFixture fixture)
    : WebApplicationFactory<PaymentsApi::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Database", fixture.ConnectionString);
    }
}
