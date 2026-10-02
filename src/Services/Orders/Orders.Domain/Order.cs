namespace Luna.Orders.Domain;

public sealed class Order
{
    private readonly List<OrderItem> items = [];

    private Order()
    {
    }

    private Order(
        Guid id,
        Guid customerId,
        Guid? shippingQuoteId,
        string shippingMethodCode,
        decimal shippingCost,
        ShippingAddress shippingAddress,
        string idempotencyKey)
    {
        Id = id;
        CustomerId = customerId;
        ShippingQuoteId = shippingQuoteId;
        ShippingMethodCode = shippingMethodCode;
        ShippingCost = shippingCost;
        ShippingAddress = shippingAddress;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? InventoryReservationId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? ShippingQuoteId { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string ShippingMethodCode { get; private set; } = string.Empty;
    public decimal ShippingCost { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Concurrency token. Every fulfillment transition is a read-modify-write on this aggregate, so without
    /// one, two operators acting at the same moment could both pass the state guards and both advance the
    /// shipment. Matches the tokens already present on Stock, InventoryReservation and Payment.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<OrderItem> Items => items.AsReadOnly();
    public ShippingAddress ShippingAddress { get; private set; } = null!;
    public decimal Total => items.Sum(item => item.LineTotal) + ShippingCost;

    public static Order Create(
        Guid customerId,
        IReadOnlyCollection<OrderItemSnapshot> itemSnapshots,
        ShippingAddress shippingAddress,
        string shippingMethodCode,
        decimal shippingCost,
        string idempotencyKey,
        Guid? id = null,
        Guid? shippingQuoteId = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (itemSnapshots.Count == 0)
        {
            throw new ArgumentException("At least one order item is required.", nameof(itemSnapshots));
        }

        if (string.IsNullOrWhiteSpace(shippingMethodCode))
        {
            throw new ArgumentException("Shipping method is required.", nameof(shippingMethodCode));
        }

        if (shippingCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shippingCost), "Shipping cost cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (shippingQuoteId == Guid.Empty)
        {
            throw new ArgumentException("Shipping quote ID must be valid.", nameof(shippingQuoteId));
        }

        var order = new Order(id ?? Guid.NewGuid(), customerId, shippingQuoteId, shippingMethodCode.Trim().ToUpperInvariant(), decimal.Round(shippingCost, 2), shippingAddress, idempotencyKey.Trim());
        order.items.AddRange(itemSnapshots.Select(snapshot => OrderItem.Create(
            order.Id,
            snapshot.ProductId,
            snapshot.Sku,
            snapshot.ProductName,
            snapshot.UnitPrice,
            snapshot.Quantity)));
        return order;
    }

    public void MarkPaymentFailed()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.PaymentFailed;
    }

    public void Cancel()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Cancelled;
    }

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Confirmed;
    }

    public void RecordCheckoutResult(Guid inventoryReservationId, Guid paymentId)
    {
        if (inventoryReservationId == Guid.Empty || paymentId == Guid.Empty)
        {
            throw new ArgumentException("Checkout result IDs are required.");
        }

        InventoryReservationId = inventoryReservationId;
        PaymentId = paymentId;
    }

    public void Prepare()
    {
        EnsureStatus(OrderStatus.Confirmed);
        Status = OrderStatus.Preparing;
    }

    public void MarkShipped()
    {
        EnsureStatus(OrderStatus.Preparing);
        Status = OrderStatus.Shipped;
    }

    public void RecordShipment(Guid shipmentId)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment ID is required.", nameof(shipmentId));
        }

        if (ShipmentId is not null && ShipmentId != shipmentId)
        {
            throw new InvalidOperationException("The order already has a different shipment.");
        }

        ShipmentId = shipmentId;
    }

    public void EnsureStatusForShipmentInTransit() => EnsureStatus(OrderStatus.Shipped);

    public void EnsureDeliveryAllowed() => EnsureStatus(OrderStatus.Shipped);

    public void MarkDelivered()
    {
        EnsureStatus(OrderStatus.Shipped);
        Status = OrderStatus.Delivered;
    }

    public void EnsureShipmentCreationAllowed() => EnsureStatus(OrderStatus.Preparing);

    private void EnsureStatus(params OrderStatus[] expectedStatuses)
    {
        if (!expectedStatuses.Contains(Status))
        {
            throw new InvalidOperationException($"Order {Id} must be {string.Join(" or ", expectedStatuses)} to perform this transition.");
        }
    }
}

public sealed record OrderItemSnapshot(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal UnitPrice,
    int Quantity);

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    private OrderItem(Guid id, Guid orderId, Guid productId, string sku, string productName, decimal unitPrice, int quantity)
    {
        Id = id;
        OrderId = orderId;
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        LineTotal = decimal.Round(unitPrice * quantity, 2);
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }

    internal static OrderItem Create(Guid orderId, Guid productId, string sku, string productName, decimal unitPrice, int quantity)
    {
        if (orderId == Guid.Empty || productId == Guid.Empty)
        {
            throw new ArgumentException("Order and product IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product identity is required.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        return new OrderItem(Guid.NewGuid(), orderId, productId, sku.Trim(), productName.Trim(), decimal.Round(unitPrice, 2), quantity);
    }
}

public sealed class ShippingAddress
{
    private ShippingAddress()
    {
    }

    private ShippingAddress(string fullName, string addressLine1, string? addressLine2, string city, string stateOrProvince, string postalCode, string country)
    {
        FullName = fullName;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        City = city;
        StateOrProvince = stateOrProvince;
        PostalCode = postalCode;
        Country = country;
    }

    public string FullName { get; private set; } = string.Empty;
    public string AddressLine1 { get; private set; } = string.Empty;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string StateOrProvince { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    public static ShippingAddress Create(string fullName, string addressLine1, string? addressLine2, string city, string stateOrProvince, string postalCode, string country)
    {
        var values = new[] { fullName, addressLine1, city, stateOrProvince, postalCode, country };
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A complete shipping address is required.");
        }

        return new ShippingAddress(fullName.Trim(), addressLine1.Trim(), string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim(), city.Trim(), stateOrProvince.Trim(), postalCode.Trim(), country.Trim());
    }
}

public enum OrderStatus
{
    Pending,
    PaymentFailed,
    Cancelled,
    Confirmed,
    Preparing,
    Shipped,
    Delivered,
}
