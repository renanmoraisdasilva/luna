using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Domain;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShipmentWriteRepository(ShippingDbContext db) : IShipmentWriteRepository
{
    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
