using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Domain;

namespace Luna.Shipping.Infrastructure.Repositories;

public sealed class ShippingQuoteWriteRepository(ShippingDbContext db) : IShippingQuoteWriteRepository
{
    public async Task AddAsync(ShippingQuote quote, CancellationToken cancellationToken)
    {
        db.ShippingQuotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
    }
}
