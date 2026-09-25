namespace Luna.Shipping.Contracts;

public sealed record ShippingMethodResponse(
    Guid Id,
    string Code,
    string Name,
    decimal Cost,
    int EstimatedDeliveryDays);

public sealed record GetShippingMethodsResponse(IReadOnlyCollection<ShippingMethodResponse> Methods);

public sealed record QuoteShippingRequest(
    Guid OrderId,
    string ShippingMethodCode,
    string Country,
    string PostalCode);

public sealed record ShippingQuoteResponse(
    Guid QuoteId,
    Guid OrderId,
    string ShippingMethodCode,
    decimal Cost,
    int EstimatedDeliveryDays,
    DateTimeOffset CreatedAt);

public sealed record ShipmentRecipientRequest(
    Guid CustomerId,
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);

public sealed record ShipmentRecipientResponse(
    Guid CustomerId,
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);

public sealed record CreateShipmentRequest(Guid OrderId, Guid QuoteId, ShipmentRecipientRequest Recipient);

public sealed record ShipmentResponse(
    Guid ShipmentId,
    Guid OrderId,
    Guid QuoteId,
    string Status,
    string TrackingNumber,
    DateTimeOffset? DeliveredAt,
    ShipmentRecipientResponse Recipient);
