namespace Luna.Orders.Domain;

public sealed class Cart
{
    private readonly List<CartItem> items = [];

    private Cart()
    {
    }

    private Cart(Guid id, Guid customerId)
    {
        Id = id;
        CustomerId = customerId;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public IReadOnlyCollection<CartItem> Items => items.AsReadOnly();

    public static Cart Create(Guid customerId, Guid? id = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        return new Cart(id ?? Guid.NewGuid(), customerId);
    }

    public void AddItem(Guid productId, int quantity)
    {
        ValidateProduct(productId);
        ValidateQuantity(quantity);

        var existingItem = items.SingleOrDefault(item => item.ProductId == productId);
        if (existingItem is null)
        {
            items.Add(new CartItem(Guid.NewGuid(), Id, productId, quantity));
            return;
        }

        existingItem.IncreaseQuantity(quantity);
    }

    public void ChangeItemQuantity(Guid productId, int quantity)
    {
        ValidateProduct(productId);
        ValidateQuantity(quantity);
        FindItem(productId).ChangeQuantity(quantity);
    }

    public void RemoveItem(Guid productId)
    {
        ValidateProduct(productId);
        items.Remove(FindItem(productId));
    }

    private CartItem FindItem(Guid productId) =>
        items.SingleOrDefault(item => item.ProductId == productId)
        ?? throw new KeyNotFoundException("The product is not in the cart.");

    private static void ValidateProduct(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID is required.", nameof(productId));
        }
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }
    }
}
