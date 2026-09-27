using System.Net;
using FluentAssertions;
using Luna.IntegrationTests.Catalog;
using Luna.IntegrationTests.Inventory;
using Luna.IntegrationTests.Payments;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(CheckoutCommerceCollection.Name)]
public sealed class ServiceHealthTests(
    CatalogSqlServerFixture catalogFixture,
    InventorySqlServerFixture inventoryFixture,
    PaymentsSqlServerFixture paymentsFixture)
{
    [Fact]
    public async Task Catalog_health_endpoint_responds()
    {
        using var factory = new CatalogApiFactory(catalogFixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inventory_health_endpoint_responds()
    {
        using var factory = new InventoryApiFactory(inventoryFixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Payments_health_endpoint_responds()
    {
        using var factory = new PaymentsApiFactory(paymentsFixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
