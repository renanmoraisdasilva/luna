using FluentAssertions;
using Luna.Orders.Application.Orders;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class FulfillmentCommandTests
{
    [Fact]
    public async Task Create_shipment_marks_a_preparing_order_as_shipped()
    {
        var quoteId = Guid.NewGuid();
        var order = CreateOrder(OrderStatus.Preparing, quoteId);
        var repository = new FakeOrderRepository(order);
        var shipping = new FakeShippingFulfillmentClient();
        var handler = new CreateShipmentHandler(repository, shipping);

        var response = await handler.HandleAsync(new CreateShipmentCommand(order.Id), CancellationToken.None);

        response!.OrderStatus.Should().Be(nameof(OrderStatus.Shipped));
        order.Status.Should().Be(OrderStatus.Shipped);
        shipping.OrderId.Should().Be(order.Id);
        shipping.ShippingQuoteId.Should().Be(quoteId);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_shipment_marks_a_failed_attempt_for_retry()
    {
        var order = CreateOrder(OrderStatus.Preparing, Guid.NewGuid());
        var repository = new FakeOrderRepository(order);
        var handler = new CreateShipmentHandler(repository, new FakeShippingFulfillmentClient { Failure = true });

        var act = () => handler.HandleAsync(new CreateShipmentCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Shipping unavailable.");
        order.Status.Should().Be(OrderStatus.ShippingPendingRetry);
        repository.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_shipment_can_retry_a_pending_order()
    {
        var order = CreateOrder(OrderStatus.ShippingPendingRetry, Guid.NewGuid());
        var repository = new FakeOrderRepository(order);
        var handler = new CreateShipmentHandler(repository, new FakeShippingFulfillmentClient());

        var response = await handler.HandleAsync(new CreateShipmentCommand(order.Id), CancellationToken.None);

        response!.OrderStatus.Should().Be(nameof(OrderStatus.Shipped));
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task Create_shipment_rejects_an_order_that_is_not_ready()
    {
        var order = CreateOrder(OrderStatus.Confirmed, Guid.NewGuid());
        var shipping = new FakeShippingFulfillmentClient();
        var handler = new CreateShipmentHandler(new FakeOrderRepository(order), shipping);

        var act = () => handler.HandleAsync(new CreateShipmentCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        shipping.OrderId.Should().BeNull();
    }

    private static Order CreateOrder(OrderStatus status, Guid quoteId)
    {
        var order = Order.Create(
            Guid.NewGuid(),
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-001", "Test product", 25, 1)],
            ShippingAddress.Create("Jane Operator", "1 Test Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            5,
            "fulfillment-test",
            shippingQuoteId: quoteId);
        if (status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.ShippingPendingRetry)
        {
            order.Confirm();
        }

        if (status is OrderStatus.Preparing or OrderStatus.ShippingPendingRetry)
        {
            order.Prepare();
        }

        if (status == OrderStatus.ShippingPendingRetry)
        {
            order.MarkShippingPendingRetry();
        }

        return order;
    }

    private sealed class FakeOrderRepository(Order order) : IOrderWriteRepository
    {
        public int SaveCount { get; private set; }

        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) => Task.FromResult<Order?>(order.Id == orderId ? order : null);
        public Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult<Order?>(null);
        public Task AddAsync(Order order, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShippingFulfillmentClient : IShippingFulfillmentClient
    {
        public bool Failure { get; init; }
        public Guid? OrderId { get; private set; }
        public Guid? ShippingQuoteId { get; private set; }

        public Task<ShipmentSnapshot> CreateShipmentAsync(Guid orderId, Guid shippingQuoteId, CancellationToken cancellationToken)
        {
            OrderId = orderId;
            ShippingQuoteId = shippingQuoteId;
            if (Failure)
            {
                throw new InvalidOperationException("Shipping unavailable.");
            }

            return Task.FromResult(new ShipmentSnapshot(Guid.NewGuid(), orderId, shippingQuoteId, "Created", "LUNA-TEST"));
        }
    }
}
