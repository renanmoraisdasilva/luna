extern alias PaymentsApi;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Luna.IntegrationTests.Payments;

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
