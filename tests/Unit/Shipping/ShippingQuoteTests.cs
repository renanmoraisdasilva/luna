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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Quote_requires_a_shipping_method_code(string shippingMethodCode)
    {
        var act = () => ShippingQuote.Create(Guid.NewGuid(), shippingMethodCode, 5.99m, 5, "US", "90210");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Quote_rejects_negative_cost()
    {
        var act = () => ShippingQuote.Create(Guid.NewGuid(), "STANDARD", -1m, 5, "US", "90210");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Quote_rejects_non_positive_delivery_time()
    {
        var act = () => ShippingQuote.Create(Guid.NewGuid(), "STANDARD", 5.99m, 0, "US", "90210");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("", "90210")]
    [InlineData("US", "")]
    public void Quote_requires_country_and_postal_code(string country, string postalCode)
    {
        var act = () => ShippingQuote.Create(Guid.NewGuid(), "STANDARD", 5.99m, 5, country, postalCode);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Quote_uses_the_supplied_id_and_creation_time()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var quote = ShippingQuote.Create(
            Guid.NewGuid(), "STANDARD", 5.99m, 5, "US", "90210", id, createdAt);

        quote.Id.Should().Be(id);
        quote.CreatedAt.Should().Be(createdAt);
    }
}
