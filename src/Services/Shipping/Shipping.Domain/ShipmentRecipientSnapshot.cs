namespace Luna.Shipping.Domain;

public sealed class ShipmentRecipientSnapshot
{
    private ShipmentRecipientSnapshot()
    {
    }

    private ShipmentRecipientSnapshot(
        Guid customerId,
        string fullName,
        string addressLine1,
        string? addressLine2,
        string city,
        string stateOrProvince,
        string postalCode,
        string country)
    {
        CustomerId = customerId;
        FullName = fullName;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        StateOrProvince = stateOrProvince;
        PostalCode = postalCode;
        Country = country;
    }

    public Guid CustomerId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string AddressLine1 { get; private set; } = string.Empty;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string StateOrProvince { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    public static ShipmentRecipientSnapshot Create(
        Guid customerId,
        string fullName,
        string addressLine1,
        string? addressLine2,
        string city,
        string stateOrProvince,
        string postalCode,
        string country)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        var values = new[] { fullName, addressLine1, city, stateOrProvince, postalCode, country };
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A complete shipment recipient address is required.");
        }

        return new ShipmentRecipientSnapshot(
            customerId,
            fullName.Trim(),
            addressLine1.Trim(),
            string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim(),
            city.Trim(),
            stateOrProvince.Trim(),
            postalCode.Trim(),
            country.Trim());
    }
}
