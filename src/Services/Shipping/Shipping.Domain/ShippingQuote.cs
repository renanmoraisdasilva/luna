namespace Luna.Shipping.Domain;

public sealed class ShippingQuote
{
    private ShippingQuote()
    {
    }

    private ShippingQuote(
        Guid id,
        Guid orderId,
        string shippingMethodCode,
        string country,
        string postalCode,
        decimal cost,
        int estimatedDeliveryDays,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrderId = orderId;
        ShippingMethodCode = shippingMethodCode;
        Country = country;
        PostalCode = postalCode;
        Cost = cost;
        EstimatedDeliveryDays = estimatedDeliveryDays;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ShippingMethodCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public decimal Cost { get; private set; }
    public int EstimatedDeliveryDays { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ShippingQuote Create(
        Guid orderId,
        string shippingMethodCode,
        decimal cost,
        int estimatedDeliveryDays,
        string country,
        string postalCode,
        Guid? id = null,
        DateTimeOffset? createdAt = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(shippingMethodCode))
        {
            throw new ArgumentException("Shipping method code is required.", nameof(shippingMethodCode));
        }

        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Shipping cost cannot be negative.");
        }

        if (estimatedDeliveryDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDeliveryDays), "Delivery time must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ArgumentException("Country and postal code are required.");
        }

        return new ShippingQuote(
            id ?? Guid.NewGuid(),
            orderId,
            shippingMethodCode.Trim().ToUpperInvariant(),
            country.Trim().ToUpperInvariant(),
            postalCode.Trim(),
            cost,
            estimatedDeliveryDays,
            createdAt ?? DateTimeOffset.UtcNow);
    }
}
