namespace Luna.Shipping.Application.Shipments;

public interface IShipmentWriteRepository
{
    Task AddAsync(Luna.Shipping.Domain.Shipment shipment, CancellationToken cancellationToken);
}
