using Luna.Shipping.Application.Shipments;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShippingQuoteReadRepository(ShippingDbContext db) : IShippingQuoteReadRepository
{
    public async Task<ShippingQuoteReadModel> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken) =>
        await db.ShippingQuotes
            .Where(quote => quote.Id == quoteId)
            .Select(quote => new ShippingQuoteReadModel(
                quote.Id,
                quote.OrderId,
                quote.ShippingMethodCode,
                quote.Cost,
                quote.EstimatedDeliveryDays,
                quote.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new KeyNotFoundException("The requested shipping quote was not found.");
}
