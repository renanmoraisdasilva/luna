namespace Luna.Orders.Application.Orders;

public sealed record ShipmentSnapshot(
    Guid ShipmentId,
    Guid OrderId,
    Guid QuoteId,
    string Status,
    string TrackingNumber);

public interface IShippingFulfillmentClient
{
    Task<ShipmentSnapshot> CreateShipmentAsync(
        Guid orderId,
        Guid shippingQuoteId,
        CancellationToken cancellationToken);
}
