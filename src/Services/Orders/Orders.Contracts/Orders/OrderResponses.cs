namespace Luna.Orders.Contracts.Orders;

public sealed record OrderSummaryResponse(
    Guid Id,
    string Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    int ItemCount);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string ShippingMethodCode,
    DateTimeOffset CreatedAt,
    ShippingAddressResponse ShippingAddress,
    IReadOnlyCollection<OrderItemResponse> Items);

public sealed record OrderItemResponse(
    Guid ProductId,
    string Sku,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record ShippingAddressResponse(
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);
