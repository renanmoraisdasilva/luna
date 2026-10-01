using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.IntegrationTests.Orders;
using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.IntegrationTests.Shipping;

[Collection(ShippingDatabaseCollection.Name)]
public sealed class ShipmentsOperationsTests(ShippingSqlServerFixture fixture) : IAsyncLifetime
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
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_requires_an_operations_role()
    {
        using var factory = new ShippingApiFactory(fixture);

        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/v1/shipments");

        using var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());
        var customerResponse = await customerClient.GetAsync("/api/v1/shipments");

        using var wrongRoleClient = factory.CreateClient();
        wrongRoleClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "Operator");
        var wrongRoleResponse = await wrongRoleClient.GetAsync("/api/v1/shipments");

        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongRoleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_accepts_a_standard_role_claim_as_well_as_the_luna_role_claim()
    {
        using var factory = new ShippingApiFactory(fixture);

        using var lunaRoleClient = factory.CreateClient();
        lunaRoleClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");
        var lunaRoleResponse = await lunaRoleClient.GetAsync("/api/v1/shipments");

        using var standardRoleClient = factory.CreateClient();
        standardRoleClient.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeader, "admin");
        var standardRoleResponse = await standardRoleClient.GetAsync("/api/v1/shipments");

        lunaRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        standardRoleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_filters_by_status_search_text_and_paging()
    {
        var created = await SeedShipmentAsync("Jane Doe", "Austin", ShipmentStatus.Created);
        var inTransit = await SeedShipmentAsync("John Roe", "Dallas", ShipmentStatus.InTransit);
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var allResponse = await client.GetAsync("/api/v1/shipments?pageSize=10");
        var all = await allResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        var statusResponse = await client.GetAsync("/api/v1/shipments?status=InTransit");
        var byStatus = await statusResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        var multiStatusResponse = await client.GetAsync("/api/v1/shipments?status=Created,InTransit");
        var byMultiStatus = await multiStatusResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        var searchResponse = await client.GetAsync("/api/v1/shipments?search=Dallas");
        var bySearch = await searchResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        var shipmentIdResponse = await client.GetAsync($"/api/v1/shipments?search={created.Id}");
        var byShipmentId = await shipmentIdResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        var customerIdResponse = await client.GetAsync($"/api/v1/shipments?search={created.Recipient.CustomerId}");
        var byCustomerId = await customerIdResponse.Content.ReadFromJsonAsync<ShipmentListResponse>();

        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        all!.TotalCount.Should().Be(2);
        all.Items.Single(item => item.ShipmentId == created.Id).AvailableAction.Should().Be("MarkInTransit");
        all.Items.Single(item => item.ShipmentId == created.Id).InTransitAt.Should().BeNull();
        all.Items.Single(item => item.ShipmentId == inTransit.Id).AvailableAction.Should().Be("MarkDelivered");
        all.Items.Single(item => item.ShipmentId == inTransit.Id).InTransitAt.Should().NotBeNull();
        byStatus!.Items.Single().ShipmentId.Should().Be(inTransit.Id);
        byMultiStatus!.TotalCount.Should().Be(2);
        bySearch!.Items.Single().ShipmentId.Should().Be(inTransit.Id);
        byShipmentId!.Items.Single().ShipmentId.Should().Be(created.Id);
        byCustomerId!.Items.Single().ShipmentId.Should().Be(created.Id);
    }

    [Theory]
    [InlineData("status=Bogus")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task List_rejects_invalid_filters(string query)
    {
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/shipments?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("INVALID_SHIPPING_REQUEST");
        error.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Detail_returns_the_recipient_and_reports_missing_shipments()
    {
        var delivered = await SeedShipmentAsync("Jane Doe", "Austin", ShipmentStatus.Delivered);
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var detailResponse = await client.GetAsync($"/api/v1/shipments/{delivered.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ShipmentDetailResponse>();
        var missingResponse = await client.GetAsync($"/api/v1/shipments/{Guid.NewGuid()}");

        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        detail!.ShipmentId.Should().Be(delivered.Id);
        detail.Status.Should().Be(nameof(ShipmentStatus.Delivered));
        detail.AvailableAction.Should().Be("None");
        detail.InTransitAt.Should().NotBeNull();
        detail.Recipient.FullName.Should().Be("Jane Doe");
        detail.TrackingEvents.Should().HaveCount(3);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tracking_requires_a_resolvable_customer_subject()
    {
        var shipment = await SeedShipmentAsync("Jane Doe", "Austin", ShipmentStatus.Created);
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.AdminHeader, "true");

        var response = await client.GetAsync($"/api/v1/shipments/{shipment.Id}/tracking");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        error!.Code.Should().Be("SHIPPING_CONFLICT");
    }

    [Fact]
    public async Task Lifecycle_endpoints_return_not_found_for_unknown_shipments()
    {
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ServiceHeader, "orders");
        var unknownShipmentId = Guid.NewGuid();

        var inTransitResponse = await client.PostAsync($"/api/v1/shipments/{unknownShipmentId}/in-transit", content: null);
        var deliveredResponse = await client.PostAsync($"/api/v1/shipments/{unknownShipmentId}/delivered", content: null);

        inTransitResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deliveredResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Shipment> SeedShipmentAsync(string fullName, string city, ShipmentStatus status)
    {
        var shipment = Shipment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"LUNA-OPS-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            ShipmentRecipientSnapshot.Create(
                Guid.NewGuid(),
                fullName,
                "1 Main Street",
                null,
                city,
                "Texas",
                "78701",
                "US"));

        if (status is ShipmentStatus.InTransit or ShipmentStatus.Delivered)
        {
            shipment.MarkInTransit();
        }

        if (status is ShipmentStatus.Delivered)
        {
            shipment.MarkDelivered(DateTimeOffset.UtcNow);
        }

        await using var db = fixture.CreateDbContext();
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return shipment;
    }

    private sealed record ErrorDto(string Code, string Message);
}
