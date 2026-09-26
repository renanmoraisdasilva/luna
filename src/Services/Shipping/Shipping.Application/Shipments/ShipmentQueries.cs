using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;

namespace Luna.Shipping.Application.Shipments;

public sealed record ShipmentQuery(string? Status, string? Search, int Page, int PageSize);

public interface IShipmentReadRepository
{
    Task<ShipmentListResponse> GetListAsync(ShipmentQuery query, CancellationToken cancellationToken);
    Task<ShipmentDetailResponse?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task<ShipmentTrackingResponse?> GetTrackingByIdAsync(Guid shipmentId, Guid customerId, CancellationToken cancellationToken);
}

public sealed class GetShipmentsHandler(IShipmentReadRepository repository)
{
    public Task<ShipmentListResponse> HandleAsync(ShipmentQuery query, CancellationToken cancellationToken) =>
        repository.GetListAsync(query, cancellationToken);
}

public sealed class GetShipmentHandler(IShipmentReadRepository repository)
{
    public Task<ShipmentDetailResponse?> HandleAsync(Guid shipmentId, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(shipmentId, cancellationToken);
}

public static class ShipmentResponseMapper
{
    public static ShipmentResponse Map(Shipment shipment) => new(
        shipment.Id,
        shipment.OrderId,
        shipment.QuoteId,
        shipment.Status.ToString(),
        shipment.TrackingNumber,
        shipment.DeliveredAt,
        new ShipmentRecipientResponse(
            shipment.Recipient.CustomerId,
            shipment.Recipient.FullName,
            shipment.Recipient.AddressLine1,
            shipment.Recipient.AddressLine2,
            shipment.Recipient.City,
            shipment.Recipient.StateOrProvince,
            shipment.Recipient.PostalCode,
            shipment.Recipient.Country));

    public static string AvailableAction(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Created => "MarkInTransit",
        ShipmentStatus.InTransit => "MarkDelivered",
        _ => "None",
    };
}

public sealed class GetShipmentTrackingHandler(IShipmentReadRepository repository)
{
    public Task<ShipmentTrackingResponse?> HandleAsync(
        Guid shipmentId,
        Guid customerId,
        CancellationToken cancellationToken) =>
        repository.GetTrackingByIdAsync(shipmentId, customerId, cancellationToken);
}
