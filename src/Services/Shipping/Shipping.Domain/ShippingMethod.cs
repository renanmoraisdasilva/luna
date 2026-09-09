namespace Luna.Shipping.Domain;

public sealed class ShippingMethod
{
    private ShippingMethod()
    {
    }

    private ShippingMethod(Guid id, string code, string name, decimal cost, int estimatedDeliveryDays)
    {
        Id = id;
        Code = code;
        Name = name;
        Cost = cost;
        EstimatedDeliveryDays = estimatedDeliveryDays;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal Cost { get; private set; }
    public int EstimatedDeliveryDays { get; private set; }

    public static ShippingMethod Create(
        string code,
        string name,
        decimal cost,
        int estimatedDeliveryDays,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shipping method code and name are required.");
        }

        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Shipping cost cannot be negative.");
        }

        if (estimatedDeliveryDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDeliveryDays), "Delivery time must be greater than zero.");
        }

        return new ShippingMethod(id ?? Guid.NewGuid(), code.Trim().ToUpperInvariant(), name.Trim(), cost, estimatedDeliveryDays);
    }
}
