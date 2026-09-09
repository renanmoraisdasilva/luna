using FluentAssertions;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShippingQuoteTests
{
    [Fact]
    public void Quote_snapshots_the_shipping_method_price_and_delivery_time()
    {
        var method = ShippingMethod.Create("EXPRESS", "Express", 14.99m, 2);
        var quote = ShippingQuote.Create(Guid.NewGuid(), method.Code, method.Cost, method.EstimatedDeliveryDays, "us", "90210");

        quote.ShippingMethodCode.Should().Be("EXPRESS");
        quote.Cost.Should().Be(14.99m);
        quote.EstimatedDeliveryDays.Should().Be(2);
        quote.Country.Should().Be("US");
    }

    [Fact]
    public void Quote_requires_an_order_and_address()
    {
        var method = ShippingMethod.Create("STANDARD", "Standard", 5.99m, 5);

        var act = () => ShippingQuote.Create(Guid.Empty, method.Code, method.Cost, method.EstimatedDeliveryDays, "US", "90210");

        act.Should().Throw<ArgumentException>();
    }
}
