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
    public void Preparing_order_can_be_marked_for_shipping_retry()
    {
        var order = Order.Create(CustomerId, ValidItems(), Address(), "STANDARD", 0, "shipping-retry");

        order.Confirm();
        order.Prepare();
        order.MarkShippingPendingRetry();

        order.Status.Should().Be(OrderStatus.ShippingPendingRetry);
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
            order.MarkShippingPendingRetry,
        };

        actions.Should().OnlyContain(action => ThrowsInvalidOperation(action));
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
