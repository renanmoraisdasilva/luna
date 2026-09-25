using Luna.Shipping.Domain;

namespace Luna.Shipping.Application.Shipments;

public interface IShipmentWriteRepository
{
    Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task<Shipment?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken);
    Task AddAsync(Luna.Shipping.Domain.Shipment shipment, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
