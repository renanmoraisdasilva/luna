using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShippingMethodReadRepository(ShippingDbContext db) : IShippingMethodReadRepository
{
    public async Task<IReadOnlyCollection<ShippingMethodReadModel>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.ShippingMethods
            .OrderBy(method => method.Cost)
            .Select(method => new ShippingMethodReadModel(
                method.Id,
                method.Code,
                method.Name,
                method.Cost,
                method.EstimatedDeliveryDays))
            .ToArrayAsync(cancellationToken);

    public async Task<ShippingMethodReadModel> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        await db.ShippingMethods
            .Where(method => method.Code == code.Trim().ToUpper())
            .Select(method => new ShippingMethodReadModel(
                method.Id,
                method.Code,
                method.Name,
                method.Cost,
                method.EstimatedDeliveryDays))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new KeyNotFoundException("The requested shipping method was not found.");
}
