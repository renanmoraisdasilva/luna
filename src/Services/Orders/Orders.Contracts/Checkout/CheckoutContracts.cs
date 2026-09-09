namespace Luna.Orders.Contracts.Checkout;

public sealed record CheckoutRequest(
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country,
    string ShippingMethodCode,
    string PaymentMethod,
    string Currency);

public sealed record CheckoutResponse(
    Guid OrderId,
    string Status,
    decimal Total,
    string Currency,
    Guid ReservationId,
    Guid PaymentId);
