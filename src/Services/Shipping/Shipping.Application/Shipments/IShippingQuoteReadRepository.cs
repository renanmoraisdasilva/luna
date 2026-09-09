namespace Luna.Shipping.Application.Shipments;

public sealed record ShippingQuoteReadModel(
    Guid Id,
    Guid OrderId,
    string ShippingMethodCode,
    decimal Cost,
    int EstimatedDeliveryDays,
    DateTimeOffset CreatedAt);

public interface IShippingQuoteReadRepository
{
    Task<ShippingQuoteReadModel> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken);
}
