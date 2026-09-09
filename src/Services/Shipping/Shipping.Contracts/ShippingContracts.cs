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

public sealed record CreateShipmentRequest(Guid OrderId, Guid QuoteId);

public sealed record ShipmentResponse(
    Guid ShipmentId,
    Guid OrderId,
    Guid QuoteId,
    string Status,
    string TrackingNumber,
    DateTimeOffset? DeliveredAt);
