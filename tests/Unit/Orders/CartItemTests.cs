using FluentAssertions;
using Luna.Orders.Domain;
using Xunit;

namespace Luna.UnitTests.Orders;

public sealed class CartItemTests
{
    [Fact]
    public void Change_quantity_rejects_non_positive_values()
    {
        var item = new CartItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);

        var act = () => item.ChangeQuantity(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Increase_quantity_rejects_non_positive_values()
    {
        var item = new CartItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);

        var act = () => item.IncreaseQuantity(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Increase_quantity_adds_to_the_existing_quantity()
    {
        var item = new CartItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);

        item.IncreaseQuantity(3);

        item.Quantity.Should().Be(5);
    }
}
