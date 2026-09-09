using FluentAssertions;
using Luna.Orders.Application.Carts;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Contracts.Checkout;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class CheckoutHandlerTests
{
    [Fact]
    public async Task Successful_checkout_reserves_inventory_authorizes_payment_and_confirms_order()
    {
        var customerId = Guid.NewGuid();
        var catalog = new FakeCatalogClient();
        var shipping = new FakeShippingClient();
        var inventory = new FakeInventoryClient();
        var payments = new FakePaymentsClient(authorized: true);
        var repository = new FakeOrderRepository();
        var handler = CreateHandler(customerId, catalog, shipping, inventory, payments, repository);

        var response = await handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest()), CancellationToken.None);

        response.Status.Should().Be(nameof(OrderStatus.Confirmed));
        response.Total.Should().Be(49.99m);
        repository.Order!.Status.Should().Be(OrderStatus.Confirmed);
        inventory.ReservedOrderId.Should().Be(repository.Order.Id);
        payments.AuthorizedAmount.Should().Be(49.99m);
    }

    [Fact]
    public async Task Inventory_failure_cancels_pending_order_and_does_not_authorize_payment()
    {
        var customerId = Guid.NewGuid();
        var inventory = new FakeInventoryClient { Failure = true };
        var payments = new FakePaymentsClient(authorized: true);
        var repository = new FakeOrderRepository();
        var handler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), inventory, payments, repository);

        var act = () => handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest()), CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutRejectedException>().WithMessage("The requested inventory is not available.");
        repository.Order!.Status.Should().Be(OrderStatus.Cancelled);
        payments.AuthorizeCalls.Should().Be(0);
    }

    [Fact]
    public async Task Payment_decline_releases_inventory_and_marks_order_failed()
    {
        var customerId = Guid.NewGuid();
        var inventory = new FakeInventoryClient();
        var repository = new FakeOrderRepository();
        var handler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), inventory, new FakePaymentsClient(authorized: false), repository);

        var act = () => handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest()), CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutRejectedException>().WithMessage("Payment authorization was declined.");
        repository.Order!.Status.Should().Be(OrderStatus.PaymentFailed);
        inventory.ReleasedReservationId.Should().Be(inventory.ReservationId);
    }

    private static CheckoutHandler CreateHandler(
        Guid customerId,
        FakeCatalogClient catalog,
        FakeShippingClient shipping,
        FakeInventoryClient inventory,
        FakePaymentsClient payments,
        FakeOrderRepository repository)
    {
        var cart = new FakeCartReadRepository(new CartResponse(Guid.NewGuid(), customerId, [new CartItemResponse(catalog.Product.Id, 2)]));
        return new CheckoutHandler(cart, repository, catalog, shipping, inventory, payments);
    }

    private static CheckoutRequest ValidRequest() => new(
        "Jane Doe",
        "123 Luna Street",
        null,
        "Austin",
        "Texas",
        "78701",
        "US",
        "STANDARD",
        "test-card",
        "USD");

    private sealed class FakeCartReadRepository(CartResponse cart) : ICartReadRepository
    {
        public Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken) => Task.FromResult<CartResponse?>(cart);
    }

    private sealed class FakeOrderRepository : IOrderWriteRepository
    {
        public Order? Order { get; private set; }
        public Task AddAsync(Order order, CancellationToken cancellationToken) { Order = order; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCatalogClient : ICatalogCheckoutClient
    {
        public CatalogProductSnapshot Product { get; } = new(Guid.NewGuid(), "SKU-1", "Keyboard", 20m);
        public Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken) => Task.FromResult<CatalogProductSnapshot?>(Product);
    }

    private sealed class FakeShippingClient : IShippingCheckoutClient
    {
        public Task<ShippingQuoteSnapshot> QuoteAsync(Guid orderId, string shippingMethodCode, ShippingAddress address, CancellationToken cancellationToken) => Task.FromResult(new ShippingQuoteSnapshot(Guid.NewGuid(), shippingMethodCode, 9.99m));
    }

    private sealed class FakeInventoryClient : IInventoryCheckoutClient
    {
        public Guid ReservationId { get; } = Guid.NewGuid();
        public Guid? ReservedOrderId { get; private set; }
        public Guid? ReleasedReservationId { get; private set; }
        public bool Failure { get; init; }
        public Task<InventoryReservationSnapshot> ReserveAsync(Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken cancellationToken)
        {
            if (Failure) throw new InvalidOperationException("insufficient inventory");
            ReservedOrderId = orderId;
            return Task.FromResult(new InventoryReservationSnapshot(ReservationId, orderId, "Active"));
        }
        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken) { ReleasedReservationId = reservationId; return Task.CompletedTask; }
    }

    private sealed class FakePaymentsClient(bool authorized) : IPaymentsCheckoutClient
    {
        public int AuthorizeCalls { get; private set; }
        public decimal AuthorizedAmount { get; private set; }
        public Task<PaymentAuthorizationSnapshot> AuthorizeAsync(Guid orderId, decimal amount, string currency, string paymentMethod, CancellationToken cancellationToken)
        {
            AuthorizeCalls++;
            AuthorizedAmount = amount;
            return Task.FromResult(new PaymentAuthorizationSnapshot(Guid.NewGuid(), orderId, authorized ? "Authorized" : "Failed", authorized));
        }
    }
}
