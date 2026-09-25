namespace Luna.Shipping.Application.Shipments;

public interface IShipmentWriteRepository
{
    Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddAsync(Luna.Shipping.Domain.Shipment shipment, CancellationToken cancellationToken);
}
