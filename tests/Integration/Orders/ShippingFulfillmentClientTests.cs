using System.Net;
using System.Text;
using FluentAssertions;
using Luna.Orders.Application.Orders;
using Luna.Orders.Infrastructure.Checkout;
using Xunit;

namespace Luna.IntegrationTests.Orders;

public sealed class ShippingFulfillmentClientTests
{
    [Fact]
    public async Task Create_shipment_maps_the_shipment_snapshot_from_the_response()
    {
        var shipmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var client = CreateClient(HttpStatusCode.OK,
            $$"""{"shipmentId":"{{shipmentId}}","orderId":"{{orderId}}","quoteId":"{{quoteId}}","status":"Created","trackingNumber":"LUNA-1"}""");
        var recipient = Recipient();

        var snapshot = await client.CreateShipmentAsync(orderId, quoteId, recipient, CancellationToken.None);

        snapshot.ShipmentId.Should().Be(shipmentId);
        snapshot.OrderId.Should().Be(orderId);
        snapshot.QuoteId.Should().Be(quoteId);
        snapshot.Status.Should().Be("Created");
        snapshot.TrackingNumber.Should().Be("LUNA-1");
    }

    [Fact]
    public async Task Create_shipment_rejects_an_empty_success_payload()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var act = () => client.CreateShipmentAsync(Guid.NewGuid(), Guid.NewGuid(), Recipient(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Shipping returned an empty shipment response.");
    }

    [Fact]
    public async Task Mark_shipment_surfaces_the_conflict_message_from_the_error_payload()
    {
        var client = CreateClient(HttpStatusCode.Conflict,
            """{"code":"SHIPPING_CONFLICT","message":"Shipment must be Created before it can transition from Delivered."}""");

        var act = () => client.MarkInTransitAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Shipment must be Created before it can transition from Delivered.");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"code":"SHIPPING_CONFLICT"}""")]
    public async Task Mark_shipment_falls_back_when_the_conflict_payload_has_no_message(string body)
    {
        var client = CreateClient(HttpStatusCode.Conflict, body);

        var act = () => client.MarkDeliveredAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Shipping rejected the shipment transition.");
    }

    [Fact]
    public async Task Mark_shipment_rejects_an_empty_success_payload()
    {
        var client = CreateClient(HttpStatusCode.OK, "null");

        var act = () => client.MarkInTransitAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Shipping returned an empty shipment response.");
    }

    [Fact]
    public async Task Mark_shipment_propagates_unsuccessful_status_codes()
    {
        var client = CreateClient(HttpStatusCode.NotFound, """{"code":"SHIPPING_RESOURCE_NOT_FOUND","message":"missing"}""");

        var act = () => client.MarkInTransitAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static ShippingFulfillmentClient CreateClient(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHandler(statusCode, body);
        var client = new ShippingFulfillmentClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
        });
        return client;
    }

    private static ShipmentRecipientSnapshot Recipient() =>
        new(
            Guid.NewGuid(),
            "Jane Doe",
            "1 Main Street",
            null,
            "Austin",
            "Texas",
            "78701",
            "US");

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
