using Luna.Shipping.Domain;

namespace Luna.Shipping.Application.Quotes;

public interface IShippingQuoteWriteRepository
{
    Task AddAsync(ShippingQuote quote, CancellationToken cancellationToken);
}
