namespace Luna.Inventory.Domain;

public sealed class Stock
{
    private Stock()
    {
    }

    private Stock(Guid id, Guid productId, int quantityOnHand)
    {
        Id = id;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public int AvailableQuantity => QuantityOnHand - QuantityReserved;

    public static Stock Create(Guid productId, int quantityOnHand, Guid? id = null)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID is required.", nameof(productId));
        }

        if (quantityOnHand < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand), "Quantity on hand cannot be negative.");
        }

        return new Stock(id ?? Guid.NewGuid(), productId, quantityOnHand);
    }

    public void Reserve(int quantity)
    {
        ValidateQuantity(quantity);
        if (quantity > AvailableQuantity)
        {
            throw new InsufficientInventoryException(ProductId, quantity, AvailableQuantity);
        }

        QuantityReserved += quantity;
    }

    public void Release(int quantity)
    {
        ValidateQuantity(quantity);
        if (quantity > QuantityReserved)
        {
            throw new InvalidOperationException("Cannot release more inventory than is reserved.");
        }

        QuantityReserved -= quantity;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }
    }
}

public sealed class InsufficientInventoryException(Guid productId, int requested, int available) : Exception(
    $"Insufficient inventory for product {productId}. Requested {requested}, available {available}.")
{
    public Guid ProductId { get; } = productId;
    public int Requested { get; } = requested;
    public int Available { get; } = available;
}

public sealed class StockNotFoundException(Guid productId) : Exception(
    $"No inventory stock exists for product {productId}.")
{
    public Guid ProductId { get; } = productId;
}
