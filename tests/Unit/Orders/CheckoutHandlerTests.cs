using FluentAssertions;
using Luna.Orders.Application.Carts;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Application.Orders;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Contracts.Checkout;
using Luna.Orders.Domain;
using Microsoft.Extensions.Logging.Abstractions;
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

        var response = await handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-1"), CancellationToken.None);

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

        var act = () => handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-2"), CancellationToken.None);

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

        var act = () => handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-3"), CancellationToken.None);

        await act.Should().ThrowAsync<CheckoutRejectedException>().WithMessage("Payment authorization was declined.");
        repository.Order!.Status.Should().Be(OrderStatus.PaymentFailed);
        inventory.ReleasedReservationId.Should().Be(inventory.ReservationId);
    }

    [Fact]
    public async Task Replaying_confirmed_checkout_returns_original_result_without_side_effects()
    {
        var customerId = Guid.NewGuid();
        var idempotencyKey = "checkout-replay";
        var existingOrder = Order.Create(
            customerId,
            [new OrderItemSnapshot(Guid.NewGuid(), "SKU-1", "Keyboard", 20m, 2)],
            ShippingAddress.Create("Jane Doe", "123 Luna Street", null, "Austin", "Texas", "78701", "US"),
            "STANDARD",
            9.99m,
            idempotencyKey,
            Guid.NewGuid());
        existingOrder.RecordCheckoutResult(Guid.NewGuid(), Guid.NewGuid());
        existingOrder.Confirm();
        var repository = new FakeOrderRepository { ExistingOrder = existingOrder };
        var inventory = new FakeInventoryClient();
        var payments = new FakePaymentsClient(authorized: true);
        var handler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), inventory, payments, repository);

        var response = await handler.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), idempotencyKey), CancellationToken.None);

        response.OrderId.Should().Be(existingOrder.Id);
        response.Status.Should().Be(nameof(OrderStatus.Confirmed));
        response.Total.Should().Be(existingOrder.Total);
        response.ReservationId.Should().Be(existingOrder.InventoryReservationId!.Value);
        response.PaymentId.Should().Be(existingOrder.PaymentId!.Value);
        inventory.ReservedOrderId.Should().BeNull();
        payments.AuthorizeCalls.Should().Be(0);
    }

    [Fact]
    public async Task Rejects_empty_customer_id()
    {
        var handler = CreateHandler(Guid.NewGuid());

        await handler.Invoking(value => value.HandleAsync(new CheckoutCommand(Guid.Empty, ValidRequest(), "checkout-4"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage("Customer ID is required.");
    }

    [Fact]
    public async Task Rejects_missing_or_empty_cart()
    {
        var customerId = Guid.NewGuid();
        var missingCartHandler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), new FakeInventoryClient(), new FakePaymentsClient(true), new FakeOrderRepository(), null, false);
        await missingCartHandler.Invoking(value => value.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-5"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage("The cart must contain at least one item.");

        var emptyCart = new CartResponse(Guid.NewGuid(), customerId, []);
        var emptyCartHandler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), new FakeInventoryClient(), new FakePaymentsClient(true), new FakeOrderRepository(), emptyCart);
        await emptyCartHandler.Invoking(value => value.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-6"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage("The cart must contain at least one item.");
    }

    [Theory]
    [InlineData("", "USD", "Payment method is required.")]
    [InlineData("card", "US", "Currency must be a three-letter code.")]
    public async Task Rejects_invalid_payment_details(string paymentMethod, string currency, string message)
    {
        var request = ValidRequest() with { PaymentMethod = paymentMethod, Currency = currency };
        var handler = CreateHandler(Guid.NewGuid());

        await handler.Invoking(value => value.HandleAsync(new CheckoutCommand(Guid.NewGuid(), request, "checkout-7"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage(message);
    }

    [Fact]
    public async Task Rejects_when_a_product_is_unavailable()
    {
        var customerId = Guid.NewGuid();
        var handler = CreateHandler(customerId, new FakeCatalogClient { Missing = true }, new FakeShippingClient(), new FakeInventoryClient(), new FakePaymentsClient(true), new FakeOrderRepository());

        await handler.Invoking(value => value.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-8"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage("Product * is no longer available.");
    }

    [Fact]
    public async Task Reports_release_failure_when_payment_fails()
    {
        var customerId = Guid.NewGuid();
        var inventory = new FakeInventoryClient { ReleaseFailure = true };
        var handler = CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), inventory, new FakePaymentsClient(authorized: false), new FakeOrderRepository());

        await handler.Invoking(value => value.HandleAsync(new CheckoutCommand(customerId, ValidRequest(), "checkout-9"), CancellationToken.None))
            .Should().ThrowAsync<CheckoutRejectedException>()
            .WithMessage("Payment failed and the inventory reservation could not be released.");
    }

    private static CheckoutHandler CreateHandler(
        Guid customerId,
        FakeCatalogClient catalog,
        FakeShippingClient shipping,
        FakeInventoryClient inventory,
        FakePaymentsClient payments,
        FakeOrderRepository repository,
        CartResponse? cart = null,
        bool useDefaultCart = true)
    {
        var resolvedCart = cart ?? (useDefaultCart ? new CartResponse(Guid.NewGuid(), customerId, [new CartItemResponse(catalog.Product.Id, 2)]) : null);
        var cartRepository = new FakeCartReadRepository(resolvedCart);
        return new CheckoutHandler(cartRepository, repository, catalog, shipping, inventory, payments, NullLogger<CheckoutHandler>.Instance);
    }

    private static CheckoutHandler CreateHandler(Guid customerId) =>
        CreateHandler(customerId, new FakeCatalogClient(), new FakeShippingClient(), new FakeInventoryClient(), new FakePaymentsClient(true), new FakeOrderRepository());

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

    private sealed class FakeCartReadRepository(CartResponse? cart) : ICartReadRepository
    {
        public Task<CartResponse?> GetCartAsync(Guid customerId, CancellationToken cancellationToken) => Task.FromResult<CartResponse?>(cart);
    }

    private sealed class FakeOrderRepository : IOrderWriteRepository
    {
        public Order? Order { get; private set; }
        public Order? ExistingOrder { get; init; }
        public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) => Task.FromResult<Order?>(null);
        public Task<Order?> GetByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(ExistingOrder);
        public Task AddAsync(Order order, CancellationToken cancellationToken) { Order = order; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCatalogClient : ICatalogCheckoutClient
    {
        public CatalogProductSnapshot Product { get; } = new(Guid.NewGuid(), "SKU-1", "Keyboard", 20m);
        public bool Missing { get; init; }
        public Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken) => Task.FromResult<CatalogProductSnapshot?>(Missing ? null : Product);
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
        public bool ReleaseFailure { get; init; }
        public Task<InventoryReservationSnapshot> ReserveAsync(Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken cancellationToken)
        {
            if (Failure) throw new InvalidOperationException("insufficient inventory");
            ReservedOrderId = orderId;
            return Task.FromResult(new InventoryReservationSnapshot(ReservationId, orderId, "Active"));
        }
        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
        {
            if (ReleaseFailure) throw new InvalidOperationException("release failed");
            ReleasedReservationId = reservationId;
            return Task.CompletedTask;
        }
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
