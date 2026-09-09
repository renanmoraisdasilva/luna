using FluentAssertions;
using Luna.Inventory.Domain;
using Xunit;

namespace Luna.UnitTests.Inventory;

public sealed class StockTests
{
    [Fact]
    public void Available_quantity_is_on_hand_minus_reserved()
    {
        var stock = Stock.Create(Guid.NewGuid(), 5);

        stock.Reserve(3);

        stock.AvailableQuantity.Should().Be(2);
        stock.QuantityReserved.Should().Be(3);
    }

    [Fact]
    public void Reservation_fails_when_available_quantity_is_insufficient()
    {
        var stock = Stock.Create(Guid.NewGuid(), 5);
        stock.Reserve(3);

        var act = () => stock.Reserve(3);

        act.Should().Throw<InsufficientInventoryException>();
        stock.QuantityReserved.Should().Be(3);
    }

    [Fact]
    public void Reservation_fails_when_quantity_is_zero()
    {
        var stock = Stock.Create(Guid.NewGuid(), 10);

        var act = () => stock.Reserve(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void Release_returns_reserved_quantity_to_available_inventory()
    {
        var stock = Stock.Create(Guid.NewGuid(), 5);
        stock.Reserve(3);

        stock.Release(3);

        stock.QuantityReserved.Should().Be(0);
        stock.AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public void Release_fails_when_quantity_exceeds_reserved_inventory()
    {
        var stock = Stock.Create(Guid.NewGuid(), 10);
        stock.Reserve(5);

        var act = () => stock.Release(6);

        act.Should().Throw<InvalidOperationException>();
        stock.QuantityReserved.Should().Be(5);
    }

    [Fact]
    public void Create_rejects_an_empty_product_id()
    {
        var act = () => Stock.Create(Guid.Empty, 10);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_negative_quantity_on_hand()
    {
        var act = () => Stock.Create(Guid.NewGuid(), -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reservation_rejects_negative_quantity()
    {
        var stock = Stock.Create(Guid.NewGuid(), 10);

        var act = () => stock.Reserve(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Release_rejects_non_positive_quantity()
    {
        var stock = Stock.Create(Guid.NewGuid(), 10);

        var act = () => stock.Release(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_uses_the_supplied_id()
    {
        var id = Guid.NewGuid();

        var stock = Stock.Create(Guid.NewGuid(), 10, id);

        stock.Id.Should().Be(id);
    }
}
