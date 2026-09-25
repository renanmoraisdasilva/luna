using System.Net.Http.Json;
using Luna.Catalog.Contracts.Products;
using Luna.Inventory.Contracts.Reservations;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Application.Orders;
using Luna.Orders.Domain;
using Luna.Payments.Contracts;
using Luna.Shipping.Contracts;

namespace Luna.Orders.Infrastructure.Checkout;

public sealed class CatalogCheckoutClient(HttpClient httpClient) : ICatalogCheckoutClient
{
    public async Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/v1/catalog/products/{productId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Catalog returned an empty product response.");
        return new CatalogProductSnapshot(product.Id, product.Sku, product.Name, product.CurrentPrice);
    }
}

public sealed class ShippingCheckoutClient(HttpClient httpClient) : IShippingCheckoutClient
{
    public async Task<ShippingQuoteSnapshot> QuoteAsync(Guid orderId, string shippingMethodCode, ShippingAddress address, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/v1/quotes", new QuoteShippingRequest(
            orderId,
            shippingMethodCode,
            address.Country,
            address.PostalCode), cancellationToken);
        response.EnsureSuccessStatusCode();
        var quote = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Shipping returned an empty quote response.");
        return new ShippingQuoteSnapshot(quote.QuoteId, quote.ShippingMethodCode, quote.Cost);
    }
}

public sealed class ShippingFulfillmentClient(HttpClient httpClient) : IShippingFulfillmentClient
{
    public async Task<ShipmentSnapshot> CreateShipmentAsync(
        Guid orderId,
        Guid shippingQuoteId,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/v1/shipments",
            new CreateShipmentRequest(orderId, shippingQuoteId),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var shipment = await response.Content.ReadFromJsonAsync<ShipmentResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Shipping returned an empty shipment response.");
        return new ShipmentSnapshot(
            shipment.ShipmentId,
            shipment.OrderId,
            shipment.QuoteId,
            shipment.Status,
            shipment.TrackingNumber);
    }
}

public sealed class InventoryCheckoutClient(HttpClient httpClient) : IInventoryCheckoutClient
{
    public async Task<InventoryReservationSnapshot> ReserveAsync(Guid orderId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/v1/reservations", new ReserveInventoryRequest(
            orderId,
            items.Select(item => new ReserveInventoryItem(item.ProductId, item.Quantity)).ToArray()), cancellationToken);
        response.EnsureSuccessStatusCode();
        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Inventory returned an empty reservation response.");
        return new InventoryReservationSnapshot(reservation.ReservationId, reservation.OrderId, reservation.Status);
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync($"api/v1/reservations/{reservationId}/release", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class PaymentsCheckoutClient(HttpClient httpClient) : IPaymentsCheckoutClient
{
    public async Task<PaymentAuthorizationSnapshot> AuthorizeAsync(Guid orderId, decimal amount, string currency, string paymentMethod, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("api/v1/payments/authorize", new AuthorizePaymentRequest(orderId, amount, currency, paymentMethod), cancellationToken);
        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<AuthorizePaymentResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Payments returned an empty authorization response.");
        return new PaymentAuthorizationSnapshot(payment.PaymentId, payment.OrderId, payment.Status, payment.Authorized);
    }
}
