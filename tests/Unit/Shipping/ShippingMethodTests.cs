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
}
