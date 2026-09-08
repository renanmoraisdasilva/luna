using FluentAssertions;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class CartTests
{
    [Fact]
    public void Create_rejects_empty_customer_id()
    {
        var act = () => Cart.Create(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Customer ID is required.*");
    }

    [Fact]
    public void Add_item_stores_product_reference_and_quantity()
    {
        var productId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid());

        cart.AddItem(productId, 2);

        cart.Items.Should().ContainSingle()
            .Which.Should().Match<CartItem>(item => item.ProductId == productId && item.Quantity == 2);
    }

    [Fact]
    public void Create_preserves_an_explicit_id()
    {
        var id = Guid.NewGuid();

        var cart = Cart.Create(Guid.NewGuid(), id);

        cart.Id.Should().Be(id);
    }

    [Fact]
    public void Adding_the_same_product_merges_quantity()
    {
        var productId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid());

        cart.AddItem(productId, 2);
        cart.AddItem(productId, 3);

        cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_item_rejects_non_positive_quantity(int quantity)
    {
        var act = () => Cart.Create(Guid.NewGuid()).AddItem(Guid.NewGuid(), quantity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cart_operations_reject_empty_product_id()
    {
        var cart = Cart.Create(Guid.NewGuid());

        var add = () => cart.AddItem(Guid.Empty, 1);
        var change = () => cart.ChangeItemQuantity(Guid.Empty, 1);
        var remove = () => cart.RemoveItem(Guid.Empty);

        add.Should().Throw<ArgumentException>();
        change.Should().Throw<ArgumentException>();
        remove.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Change_quantity_rejects_non_positive_quantity()
    {
        var productId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid());
        cart.AddItem(productId, 1);

        var act = () => cart.ChangeItemQuantity(productId, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Changing_or_removing_a_missing_item_fails()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();

        var change = () => cart.ChangeItemQuantity(productId, 1);
        var remove = () => cart.RemoveItem(productId);

        change.Should().Throw<KeyNotFoundException>();
        remove.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Change_quantity_updates_existing_item()
    {
        var productId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid());
        cart.AddItem(productId, 1);

        cart.ChangeItemQuantity(productId, 4);

        cart.Items.Single().Quantity.Should().Be(4);
    }

    [Fact]
    public void Remove_item_removes_product_reference()
    {
        var productId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid());
        cart.AddItem(productId, 1);

        cart.RemoveItem(productId);

        cart.Items.Should().BeEmpty();
    }
}
