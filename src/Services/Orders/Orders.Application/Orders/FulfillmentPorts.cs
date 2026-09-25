namespace Luna.Orders.Application.Orders;

public sealed record ShipmentSnapshot(
    Guid ShipmentId,
    Guid OrderId,
    Guid QuoteId,
    string Status,
    string TrackingNumber);

public sealed record ShipmentRecipientSnapshot(
    Guid CustomerId,
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);

public interface IShippingFulfillmentClient
{
    Task<ShipmentSnapshot> CreateShipmentAsync(
        Guid orderId,
        Guid shippingQuoteId,
        ShipmentRecipientSnapshot recipient,
        CancellationToken cancellationToken);
}
