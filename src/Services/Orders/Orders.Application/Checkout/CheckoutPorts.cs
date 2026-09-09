using Luna.Orders.Contracts.Checkout;
using Luna.Orders.Domain;

namespace Luna.Orders.Application.Checkout;

public sealed record CatalogProductSnapshot(Guid Id, string Sku, string Name, decimal CurrentPrice);

public sealed record ShippingQuoteSnapshot(Guid QuoteId, string ShippingMethodCode, decimal Cost);

public sealed record InventoryReservationSnapshot(Guid ReservationId, Guid OrderId, string Status);

public sealed record PaymentAuthorizationSnapshot(Guid PaymentId, Guid OrderId, string Status, bool Authorized);

public interface ICatalogCheckoutClient
{
    Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
}

public interface IShippingCheckoutClient
{
    Task<ShippingQuoteSnapshot> QuoteAsync(Guid orderId, string shippingMethodCode, ShippingAddress address, CancellationToken cancellationToken);
}

public interface IInventoryCheckoutClient
{
    Task<InventoryReservationSnapshot> ReserveAsync(Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);
}

public interface IPaymentsCheckoutClient
{
    Task<PaymentAuthorizationSnapshot> AuthorizeAsync(Guid orderId, decimal amount, string currency, string paymentMethod, CancellationToken cancellationToken);
}
