using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShipmentWriteRepository(ShippingDbContext db) : IShipmentWriteRepository
{
    public Task<bool> ExistsByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Shipments.AnyAsync(shipment => shipment.OrderId == orderId, cancellationToken);

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
