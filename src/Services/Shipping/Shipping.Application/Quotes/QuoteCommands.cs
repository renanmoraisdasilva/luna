using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;
using Luna.Shipping.Application.ShippingMethods;

namespace Luna.Shipping.Application.Quotes;

public sealed record QuoteShippingCommand(
    Guid OrderId,
    string ShippingMethodCode,
    string Country,
    string PostalCode);

public sealed class QuoteShippingHandler(
    IShippingMethodReadRepository methods,
    IShippingQuoteWriteRepository quotes)
{
    public async Task<ShippingQuoteResponse> HandleAsync(
        QuoteShippingCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(command.OrderId));
        }

        var method = await methods.GetByCodeAsync(command.ShippingMethodCode, cancellationToken);
        var quote = ShippingQuote.Create(
            command.OrderId,
            method.Code,
            method.Cost,
            method.EstimatedDeliveryDays,
            command.Country,
            command.PostalCode);
        await quotes.AddAsync(quote, cancellationToken);

        return new ShippingQuoteResponse(
            quote.Id,
            quote.OrderId,
            quote.ShippingMethodCode,
            quote.Cost,
            quote.EstimatedDeliveryDays,
            quote.CreatedAt);
    }
}
