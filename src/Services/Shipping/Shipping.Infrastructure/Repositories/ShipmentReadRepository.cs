using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShipmentReadRepository(ShippingDbContext db) : IShipmentReadRepository
{
    public async Task<ShipmentListResponse> GetListAsync(ShipmentQuery query, CancellationToken cancellationToken)
    {
        ValidatePaging(query.Page, query.PageSize);
        var statuses = ParseStatuses(query.Status);
        var shipmentsQuery = db.Shipments
            .AsNoTracking()
            .Include(shipment => shipment.TrackingEvents)
            .Where(shipment => statuses.Contains(shipment.Status));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var hasShipmentId = Guid.TryParse(search, out var shipmentId);
            var hasOrderId = Guid.TryParse(search, out var orderId);
            var hasCustomerId = Guid.TryParse(search, out var customerId);

            shipmentsQuery = shipmentsQuery.Where(shipment =>
                (hasShipmentId && shipment.Id == shipmentId)
                || (hasOrderId && shipment.OrderId == orderId)
                || (hasCustomerId && shipment.Recipient.CustomerId == customerId)
                || shipment.TrackingNumber.Contains(search)
                || shipment.Recipient.FullName.Contains(search)
                || shipment.Recipient.City.Contains(search));
        }

        var totalCount = await shipmentsQuery.CountAsync(cancellationToken);
        var shipments = await shipmentsQuery
            .OrderByDescending(shipment => shipment.TrackingEvents.Min(trackingEvent => trackingEvent.OccurredAt))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new ShipmentListResponse(
            shipments.Select(MapSummary).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<ShipmentDetailResponse?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await db.Shipments
            .AsNoTracking()
            .Include(value => value.TrackingEvents)
            .SingleOrDefaultAsync(value => value.Id == shipmentId, cancellationToken);

        return shipment is null ? null : MapDetail(shipment);
    }

    public async Task<ShipmentTrackingResponse?> GetTrackingByIdAsync(
        Guid shipmentId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var shipment = await db.Shipments
            .AsNoTracking()
            .Include(value => value.TrackingEvents)
            .SingleOrDefaultAsync(
                value => value.Id == shipmentId && value.Recipient.CustomerId == customerId,
                cancellationToken);

        return shipment is null ? null : MapTracking(shipment);
    }

    private static ShipmentSummaryResponse MapSummary(Shipment shipment)
    {
        var events = OrderTrackingEvents(shipment).ToArray();
        return new ShipmentSummaryResponse(
            shipment.Id,
            shipment.OrderId,
            shipment.Recipient.CustomerId,
            shipment.Recipient.FullName,
            $"{shipment.Recipient.City}, {shipment.Recipient.StateOrProvince}",
            shipment.TrackingNumber,
            shipment.Status.ToString(),
            events[0].OccurredAt,
            events.FirstOrDefault(trackingEvent => trackingEvent.Status == nameof(ShipmentStatus.InTransit))?.OccurredAt,
            shipment.DeliveredAt,
            ShipmentResponseMapper.AvailableAction(shipment.Status));
    }

    private static ShipmentDetailResponse MapDetail(Shipment shipment)
    {
        var events = OrderTrackingEvents(shipment).ToArray();
        return new ShipmentDetailResponse(
            shipment.Id,
            shipment.OrderId,
            shipment.Recipient.CustomerId,
            shipment.TrackingNumber,
            shipment.Status.ToString(),
            events[0].OccurredAt,
            events.FirstOrDefault(trackingEvent => trackingEvent.Status == nameof(ShipmentStatus.InTransit))?.OccurredAt,
            shipment.DeliveredAt,
            new ShipmentRecipientResponse(
                shipment.Recipient.CustomerId,
                shipment.Recipient.FullName,
                shipment.Recipient.AddressLine1,
                shipment.Recipient.AddressLine2,
                shipment.Recipient.City,
                shipment.Recipient.StateOrProvince,
                shipment.Recipient.PostalCode,
                shipment.Recipient.Country),
            events.Select(trackingEvent => new ShipmentTrackingEventResponse(
                trackingEvent.Id,
                trackingEvent.Status,
                trackingEvent.OccurredAt)).ToArray(),
            ShipmentResponseMapper.AvailableAction(shipment.Status));
    }

    private static ShipmentTrackingResponse MapTracking(Shipment shipment)
    {
        var events = OrderTrackingEvents(shipment).ToArray();
        return new ShipmentTrackingResponse(
            shipment.Id,
            shipment.OrderId,
            shipment.TrackingNumber,
            shipment.Status.ToString(),
            events[0].OccurredAt,
            events.FirstOrDefault(trackingEvent => trackingEvent.Status == nameof(ShipmentStatus.InTransit))?.OccurredAt,
            shipment.DeliveredAt,
            events.Select(trackingEvent => new ShipmentTrackingEventResponse(
                trackingEvent.Id,
                trackingEvent.Status,
                trackingEvent.OccurredAt)).ToArray());
    }

    private static IEnumerable<TrackingEvent> OrderTrackingEvents(Shipment shipment) =>
        shipment.TrackingEvents
            .OrderBy(trackingEvent => trackingEvent.OccurredAt)
            .ThenBy(trackingEvent => trackingEvent.Status switch
            {
                nameof(ShipmentStatus.Created) => 0,
                nameof(ShipmentStatus.InTransit) => 1,
                nameof(ShipmentStatus.Delivered) => 2,
                _ => 3,
            });

    private static IReadOnlyCollection<ShipmentStatus> ParseStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return Enum.GetValues<ShipmentStatus>();
        }

        return status
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.TryParse<ShipmentStatus>(value, true, out var parsed)
                ? parsed
                : throw new ArgumentException($"Unsupported shipment status '{value}'.", nameof(status)))
            .Distinct()
            .ToArray();
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }
    }
}
