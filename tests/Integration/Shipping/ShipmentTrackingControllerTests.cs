extern alias ShippingApi;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Authentication;
using Luna.IntegrationTests.Orders;
using Luna.Shipping.Domain;
using Luna.Shipping.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Luna.IntegrationTests.Shipping;

[Collection(ShippingDatabaseCollection.Name)]
public sealed class ShipmentTrackingControllerTests(ShippingSqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
    [Fact]
    public async Task Customer_can_read_own_tracking_without_recipient_details()
    {
        var customerId = Guid.NewGuid();
        var shipment = await SeedShipmentAsync(customerId);
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, customerId.ToString());

        var response = await client.GetAsync($"/api/v1/shipments/{shipment.Id}/tracking");
        var tracking = await response.Content.ReadFromJsonAsync<TrackingResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tracking.Should().NotBeNull();
        tracking!.ShipmentId.Should().Be(shipment.Id);
        tracking.OrderId.Should().Be(shipment.OrderId);
        tracking.TrackingNumber.Should().Be(shipment.TrackingNumber);
        tracking.Status.Should().Be(nameof(ShipmentStatus.Created));
        tracking.TrackingEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_tracking()
    {
        var ownerId = Guid.NewGuid();
        var shipment = await SeedShipmentAsync(ownerId);
        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/api/v1/shipments/{shipment.Id}/tracking");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tracking_reflects_shipment_lifecycle_events()
    {
        var customerId = Guid.NewGuid();
        var shipment = await SeedShipmentAsync(customerId);
        await using var db = fixture.CreateDbContext();
        var persistedShipment = await db.Shipments
            .Include(value => value.TrackingEvents)
            .SingleAsync(value => value.Id == shipment.Id);
        persistedShipment.MarkInTransit();
        persistedShipment.MarkDelivered(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        using var factory = new ShippingApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.CustomerHeader, customerId.ToString());

        var response = await client.GetAsync($"/api/v1/shipments/{shipment.Id}/tracking");
        var tracking = await response.Content.ReadFromJsonAsync<TrackingResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tracking!.Status.Should().Be(nameof(ShipmentStatus.Delivered));
        tracking.InTransitAt.Should().NotBeNull();
        tracking.DeliveredAt.Should().NotBeNull();
        tracking.TrackingEvents.Select(item => item.Status)
            .Should().Equal(nameof(ShipmentStatus.Created), nameof(ShipmentStatus.InTransit), nameof(ShipmentStatus.Delivered));
    }

    private async Task<Shipment> SeedShipmentAsync(Guid customerId)
    {
        var orderId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var shipment = Shipment.Create(
            orderId,
            quoteId,
            "LUNA-TRACK-001",
            ShipmentRecipientSnapshot.Create(
                customerId,
                "Jane Doe",
                "1 Main Street",
                null,
                "Austin",
                "Texas",
                "78701",
                "US"));

        await using var db = fixture.CreateDbContext();
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return shipment;
    }

    private sealed record TrackingResponse(
        Guid ShipmentId,
        Guid OrderId,
        string TrackingNumber,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? InTransitAt,
        DateTimeOffset? DeliveredAt,
        IReadOnlyCollection<TrackingEventResponse> TrackingEvents);

    private sealed record TrackingEventResponse(Guid Id, string Status, DateTimeOffset OccurredAt);
}

internal sealed class ShippingApiFactory(ShippingSqlServerFixture fixture)
    : WebApplicationFactory<ShippingApi.Program>
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
