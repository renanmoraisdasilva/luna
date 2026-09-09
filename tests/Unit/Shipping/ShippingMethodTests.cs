using FluentAssertions;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShippingMethodTests
{
    [Fact]
    public void Create_normalizes_the_method_code()
    {
        var method = ShippingMethod.Create(" standard ", "Standard", 5.99m, 5);

        method.Code.Should().Be("STANDARD");
    }

    [Fact]
    public void Create_rejects_negative_cost()
    {
        var act = () => ShippingMethod.Create("STANDARD", "Standard", -1m, 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("", "Standard")]
    [InlineData("STANDARD", "")]
    public void Create_rejects_missing_code_or_name(string code, string name)
    {
        var act = () => ShippingMethod.Create(code, name, 5.99m, 5);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rejects_non_positive_delivery_time()
    {
        var act = () => ShippingMethod.Create("STANDARD", "Standard", 5.99m, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_uses_the_supplied_id()
    {
        var id = Guid.NewGuid();

        var method = ShippingMethod.Create("STANDARD", "Standard", 5.99m, 5, id);

        method.Id.Should().Be(id);
    }
}
