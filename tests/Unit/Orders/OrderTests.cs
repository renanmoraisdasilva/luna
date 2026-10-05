using FluentAssertions;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Create_normalizes_shipping_and_item_values_and_calculates_total()
    {
        var productId = Guid.NewGuid();
        var order = Order.Create(
            CustomerId,
            [new OrderItemSnapshot(productId, " SKU-1 ", " Keyboard ", 12.345m, 2)],
            Address(),
            " standard ",
            4.567m,
            "create-normalization");

        order.Status.Should().Be(OrderStatus.Pending);
        order.ShippingMethodCode.Should().Be("STANDARD");
        order.ShippingCost.Should().Be(4.57m);
        order.Total.Should().Be(29.25m);
        order.Items.Single().Sku.Should().Be("SKU-1");
        order.Items.Single().ProductName.Should().Be("Keyboard");
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("items")]
    [InlineData("shipping")]
    public void Create_rejects_missing_required_values(string invalidValue)
    {
        var customerId = invalidValue == "customer" ? Guid.Empty : CustomerId;
        var items = invalidValue == "items"
            ? Array.Empty<OrderItemSnapshot>()
            : [new OrderItemSnapshot(Guid.NewGuid(), "SKU", "Product", 1, 1)];
        var shippingCode = invalidValue == "shipping" ? " " : "STANDARD";

        var act = () => Order.Create(customerId, items, Address(), shippingCode, 1, "required-values");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_negative_shipping_cost()
    {
        var act = () => Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", -1, "negative-shipping");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("product")]
    [InlineData("identity")]
    [InlineData("price")]
    [InlineData("quantity")]
    public void Create_rejects_invalid_item_values(string invalidValue)
    {
        var item = new OrderItemSnapshot(
            invalidValue == "product" ? Guid.Empty : Guid.NewGuid(),
            invalidValue == "identity" ? " " : "SKU",
            invalidValue == "identity" ? " " : "Product",
            invalidValue == "price" ? -1 : 1,
            invalidValue == "quantity" ? 0 : 1);

        var act = () => Order.Create(CustomerId, [item], Address(), "STANDARD", 0, "invalid-item");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Status_transitions_follow_the_order_lifecycle()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "lifecycle");

        order.Confirm();
        order.Prepare();
        order.MarkShipped();

        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Delivered_order_keeps_the_shipment_reference()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "delivered-lifecycle");
        var shipmentId = Guid.NewGuid();

        order.Confirm();
        order.Prepare();
        order.MarkShipped();
        order.RecordShipment(shipmentId);
        order.MarkDelivered();

        order.Status.Should().Be(OrderStatus.Delivered);
        order.ShipmentId.Should().Be(shipmentId);
    }

    [Theory]
    [InlineData("payment-failed")]
    [InlineData("cancelled")]
    [InlineData("confirmed")]
    public void Pending_order_supports_terminal_pending_transitions(string transition)
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "terminal-transition");

        if (transition == "payment-failed") order.MarkPaymentFailed();
        if (transition == "cancelled") order.Cancel();
        if (transition == "confirmed") order.Confirm();

        order.Status.Should().NotBe(OrderStatus.Pending);
    }

    [Fact]
    public void Invalid_status_transitions_are_rejected()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "invalid-transition");

        var actions = new Action[]
        {
            order.Prepare,
            order.MarkShipped,
        };

        actions.Should().OnlyContain(action => ThrowsInvalidOperation(action));
    }

    [Fact]
    public void Create_rejects_a_blank_idempotency_key()
    {
        var act = () => Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "   ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("idempotencyKey");
    }

    [Fact]
    public void Create_rejects_an_empty_shipping_quote_id_and_keeps_a_valid_one()
    {
        var act = () => Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "empty-quote", shippingQuoteId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("shippingQuoteId");

        var quoteId = Guid.NewGuid();
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "valid-quote", shippingQuoteId: quoteId);

        order.ShippingQuoteId.Should().Be(quoteId);
    }

    [Fact]
    public void Record_inventory_reservation_rejects_an_empty_reservation_id()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "empty-reservation");

        var act = () => order.RecordInventoryReservation(Guid.Empty);

        act.Should().Throw<ArgumentException>();
        order.InventoryReservationId.Should().BeNull();
    }

    [Fact]
    public void Record_payment_authorization_rejects_an_empty_payment_id()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "empty-payment");
        order.RecordInventoryReservation(Guid.NewGuid());

        var act = () => order.RecordPaymentAuthorization(Guid.Empty);

        act.Should().Throw<ArgumentException>();
        order.PaymentId.Should().BeNull();
    }

    [Fact]
    public void Record_payment_authorization_requires_the_reservation_first()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "out-of-order");

        var act = () => order.RecordPaymentAuthorization(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
        order.PaymentId.Should().BeNull();
    }

    [Fact]
    public void Recording_the_reservation_survives_without_the_payment_being_recorded()
    {
        // This is the state a checkout is left in if the process dies between reserving inventory and
        // authorizing payment. The reservation reference has to be durable on its own, otherwise the stock
        // is held with no record of which order holds it.
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "partial-progress");
        var reservationId = Guid.NewGuid();

        order.RecordInventoryReservation(reservationId);

        order.Status.Should().Be(OrderStatus.Pending);
        order.InventoryReservationId.Should().Be(reservationId);
        order.PaymentId.Should().BeNull();
    }

    [Fact]
    public void Each_step_refuses_to_record_once_the_order_is_confirmed()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "already-confirmed");
        order.RecordInventoryReservation(Guid.NewGuid());
        order.RecordPaymentAuthorization(Guid.NewGuid());
        order.Confirm();

        order.Invoking(item => item.RecordInventoryReservation(Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();
        order.Invoking(item => item.RecordPaymentAuthorization(Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Records_both_references_across_the_steps()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "checkout-result-valid");
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        order.RecordInventoryReservation(reservationId);
        order.RecordPaymentAuthorization(paymentId);

        order.InventoryReservationId.Should().Be(reservationId);
        order.PaymentId.Should().Be(paymentId);
    }

    [Fact]
    public void Record_shipment_rejects_an_empty_shipment_id()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "empty-shipment");

        var act = () => order.RecordShipment(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("shipmentId");
        order.ShipmentId.Should().BeNull();
    }

    [Fact]
    public void Record_shipment_accepts_a_repeated_reference_but_rejects_a_conflicting_one()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "conflicting-shipment");
        var shipmentId = Guid.NewGuid();
        order.RecordShipment(shipmentId);

        var repeat = () => order.RecordShipment(shipmentId);
        repeat.Should().NotThrow();
        order.ShipmentId.Should().Be(shipmentId);

        var conflict = () => order.RecordShipment(Guid.NewGuid());
        conflict.Should().Throw<InvalidOperationException>()
            .WithMessage("The order already has a different shipment.");
        order.ShipmentId.Should().Be(shipmentId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Address_requires_all_required_fields(bool missingValue)
    {
        var act = () => ShippingAddress.Create(
            missingValue ? " " : "Jane Doe", "123 Luna Street", null, "Austin", "Texas", "78701", "US");

        if (missingValue) act.Should().Throw<ArgumentException>();
        else act.Should().NotThrow();
    }

    [Fact]
    public void Address_trims_a_present_secondary_address_line()
    {
        var address = ShippingAddress.Create("Jane Doe", "123 Luna Street", " Suite 2 ", "Austin", "Texas", "78701", "US");

        address.AddressLine2.Should().Be("Suite 2");
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
    }

    private static OrderItemSnapshot[] ValidItems() =>
        [new OrderItemSnapshot(Guid.NewGuid(), "SKU", "Product", 1, 1)];

    private static ShippingAddress Address() =>
        ShippingAddress.Create("Jane Doe", "123 Luna Street", null, "Austin", "Texas", "78701", "US");
}
