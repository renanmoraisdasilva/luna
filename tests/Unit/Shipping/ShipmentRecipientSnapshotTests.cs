using FluentAssertions;
using Luna.Shipping.Domain;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShipmentRecipientSnapshotTests
{
    [Fact]
    public void Create_rejects_an_empty_customer_id()
    {
        var act = () => ShipmentRecipientSnapshot.Create(
            Guid.Empty,
            "Jane Doe",
            "1 Main Street",
            null,
            "Austin",
            "Texas",
            "78701",
            "US");

        act.Should().Throw<ArgumentException>().WithParameterName("customerId");
    }

    [Theory]
    [InlineData("full-name")]
    [InlineData("address-line-1")]
    [InlineData("city")]
    [InlineData("state")]
    [InlineData("postal-code")]
    [InlineData("country")]
    public void Create_rejects_any_missing_address_component(string missingValue)
    {
        var act = () => ShipmentRecipientSnapshot.Create(
            Guid.NewGuid(),
            missingValue == "full-name" ? " " : "Jane Doe",
            missingValue == "address-line-1" ? " " : "1 Main Street",
            null,
            missingValue == "city" ? " " : "Austin",
            missingValue == "state" ? " " : "Texas",
            missingValue == "postal-code" ? " " : "78701",
            missingValue == "country" ? " " : "US");

        act.Should().Throw<ArgumentException>()
            .WithMessage("A complete shipment recipient address is required.");
    }

    [Fact]
    public void Create_trims_components_and_keeps_a_present_secondary_address_line()
    {
        var customerId = Guid.NewGuid();
        var snapshot = ShipmentRecipientSnapshot.Create(
            customerId,
            " Jane Doe ",
            " 1 Main Street ",
            " Suite 2 ",
            " Austin ",
            " Texas ",
            " 78701 ",
            " US ");

        snapshot.CustomerId.Should().Be(customerId);
        snapshot.FullName.Should().Be("Jane Doe");
        snapshot.AddressLine1.Should().Be("1 Main Street");
        snapshot.AddressLine2.Should().Be("Suite 2");
        snapshot.City.Should().Be("Austin");
        snapshot.StateOrProvince.Should().Be("Texas");
        snapshot.PostalCode.Should().Be("78701");
        snapshot.Country.Should().Be("US");
    }

    [Fact]
    public void Create_normalizes_a_blank_secondary_address_line_to_null()
    {
        var snapshot = ShipmentRecipientSnapshot.Create(
            Guid.NewGuid(),
            "Jane Doe",
            "1 Main Street",
            "   ",
            "Austin",
            "Texas",
            "78701",
            "US");

        snapshot.AddressLine2.Should().BeNull();
    }
}
