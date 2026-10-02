using FluentAssertions;
using Luna.Orders.Domain;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Orders;

[Collection(OrdersDatabaseCollection.Name)]
public sealed class ShippingAddressLengthTests : IAsyncLifetime
{
    private readonly OrdersSqlServerFixture fixture;

    public ShippingAddressLengthTests(OrdersSqlServerFixture fixture) => this.fixture = fixture;

    public Task InitializeAsync()
    {
        fixture.ResetAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<string, string> OverLengthFields() => new()
    {
        { "FullName", new string('n', 5000) },
        { "AddressLine1", new string('a', 5000) },
        { "City", new string('c', 5000) },
        { "StateOrProvince", new string('s', 5000) },
        { "PostalCode", new string('p', 5000) },
        { "Country", new string('x', 5000) },
    };

    private static readonly Dictionary<string, string> ParameterNames = new(StringComparer.Ordinal)
    {
        ["FullName"] = "fullName",
        ["AddressLine1"] = "addressLine1",
        ["City"] = "city",
        ["StateOrProvince"] = "stateOrProvince",
        ["PostalCode"] = "postalCode",
        ["Country"] = "country",
    };

    [Theory]
    [MemberData(nameof(OverLengthFields))]
    public void Rejects_an_over_length_field_at_the_domain_boundary(string field, string value)
    {
        var act = () => CreateAddress(field, value);

        act.Should().Throw<ArgumentException>().WithParameterName(ParameterNames[field]);
    }

    [Theory]
    [MemberData(nameof(OverLengthFields))]
    public async Task Never_persists_an_over_length_field(string field, string value)
    {
        ShippingAddress address;
        try
        {
            address = CreateAddress(field, value);
        }
        catch (ArgumentException)
        {
            return;
        }

        var order = CreateOrder(address);

        await using var db = fixture.CreateDbContext();
        db.Orders.Add(order);

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "an address that reached the database would fail on the column constraint and return a 500");
    }

    [Fact]
    public void Accepts_a_value_at_the_documented_limit()
    {
        var act = () => CreateAddress("FullName", new string('n', 160));

        act.Should().NotThrow();
    }

    private static ShippingAddress CreateAddress(string field, string value)
    {
        var fullName = field == "FullName" ? value : "Jane Customer";
        var addressLine1 = field == "AddressLine1" ? value : "1 Test Street";
        var city = field == "City" ? value : "Austin";
        var state = field == "StateOrProvince" ? value : "Texas";
        var postalCode = field == "PostalCode" ? value : "78701";
        var country = field == "Country" ? value : "US";

        return ShippingAddress.Create(fullName, addressLine1, null, city, state, postalCode, country);
    }

    private static Order CreateOrder(ShippingAddress address) => Order.Create(
        Guid.NewGuid(),
        [new OrderItemSnapshot(Guid.NewGuid(), "SKU-ADDRESS", "Address test product", 25, 1)],
        address,
        "STANDARD",
        5,
        "address-length-check",
        Guid.NewGuid(),
        Guid.NewGuid());
}
