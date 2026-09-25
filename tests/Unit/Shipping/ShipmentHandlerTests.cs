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

        var response = await handler.HandleAsync(new CreateShipmentCommand(orderId, quoteId, Recipient()), CancellationToken.None);

        response.OrderId.Should().Be(orderId);
        response.QuoteId.Should().Be(quoteId);
        response.Status.Should().Be(nameof(ShipmentStatus.Created));
        response.TrackingNumber.Should().StartWith("LUNA-");
        response.Recipient.CustomerId.Should().Be(repository.Shipment!.Recipient.CustomerId);
        response.Recipient.FullName.Should().Be("Jane Doe");
        response.Recipient.AddressLine1.Should().Be("1 Main Street");
        repository.Shipment.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_shipment_rejects_a_quote_for_another_order()
    {
        var handler = new CreateShipmentHandler(
            new FakeQuoteRepository(Guid.NewGuid(), Guid.NewGuid()),
            new FakeShipmentRepository());

        var act = () => handler.HandleAsync(
            new CreateShipmentCommand(Guid.NewGuid(), Guid.NewGuid(), Recipient()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The shipping quote does not belong to the order.");
    }

    [Fact]
    public async Task Lifecycle_handlers_persist_in_transit_and_delivery_transitions()
    {
        var orderId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var repository = new FakeShipmentRepository();
        var createHandler = new CreateShipmentHandler(new FakeQuoteRepository(orderId, quoteId), repository);
        var created = await createHandler.HandleAsync(
            new CreateShipmentCommand(orderId, quoteId, Recipient()),
            CancellationToken.None);

        var inTransit = await new MarkShipmentInTransitHandler(repository)
            .HandleAsync(new MarkShipmentInTransitCommand(created.ShipmentId), CancellationToken.None);
        var delivered = await new MarkShipmentDeliveredHandler(repository)
            .HandleAsync(new MarkShipmentDeliveredCommand(created.ShipmentId), CancellationToken.None);

        inTransit!.Status.Should().Be(nameof(ShipmentStatus.InTransit));
        delivered!.Status.Should().Be(nameof(ShipmentStatus.Delivered));
        repository.Shipment!.TrackingEvents.Select(trackingEvent => trackingEvent.Status)
            .Should().Equal("Created", "InTransit", "Delivered");
    }

    private static ShipmentRecipientCommand Recipient() => new(
        Guid.NewGuid(),
        "Jane Doe",
        "1 Main Street",
        null,
        "Austin",
        "Texas",
        "78701",
        "US");

    private sealed class FakeQuoteRepository(Guid orderId, Guid quoteId) : IShippingQuoteReadRepository
    {
        public Task<ShippingQuoteReadModel> GetQuoteAsync(Guid requestedQuoteId, CancellationToken cancellationToken) =>
            Task.FromResult(new ShippingQuoteReadModel(quoteId, orderId, "STANDARD", 5, 3, DateTimeOffset.UtcNow));
    }

    private sealed class FakeShipmentRepository : IShipmentWriteRepository
    {
        public Shipment? Shipment { get; private set; }

        public Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Shipment?.OrderId == orderId);

        public Task<Shipment?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken) =>
            Task.FromResult(Shipment?.Id == shipmentId ? Shipment : null);

        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
        {
            Shipment = shipment;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
