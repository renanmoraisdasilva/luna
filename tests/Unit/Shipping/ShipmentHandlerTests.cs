using FluentAssertions;
using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShipmentHandlerTests
{
    [Fact]
    public async Task Create_shipment_persists_and_maps_a_new_shipment()
    {
        var orderId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var repository = new FakeShipmentRepository();
        var handler = new CreateShipmentHandler(new FakeQuoteRepository(orderId, quoteId), repository);

        var response = await handler.HandleAsync(new CreateShipmentCommand(orderId, quoteId), CancellationToken.None);

        response.OrderId.Should().Be(orderId);
        response.QuoteId.Should().Be(quoteId);
        response.Status.Should().Be(nameof(ShipmentStatus.Created));
        response.TrackingNumber.Should().StartWith("LUNA-");
        repository.Shipment.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_shipment_rejects_a_quote_for_another_order()
    {
        var handler = new CreateShipmentHandler(
            new FakeQuoteRepository(Guid.NewGuid(), Guid.NewGuid()),
            new FakeShipmentRepository());

        var act = () => handler.HandleAsync(
            new CreateShipmentCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The shipping quote does not belong to the order.");
    }

    private sealed class FakeQuoteRepository(Guid orderId, Guid quoteId) : IShippingQuoteReadRepository
    {
        public Task<ShippingQuoteReadModel> GetQuoteAsync(Guid requestedQuoteId, CancellationToken cancellationToken) =>
            Task.FromResult(new ShippingQuoteReadModel(quoteId, orderId, "STANDARD", 5, 3, DateTimeOffset.UtcNow));
    }

    private sealed class FakeShipmentRepository : IShipmentWriteRepository
    {
        public Shipment? Shipment { get; private set; }

        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
        {
            Shipment = shipment;
            return Task.CompletedTask;
        }
    }
}
