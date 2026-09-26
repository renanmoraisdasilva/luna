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
        ShipmentRecipientSnapshot recipient,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "api/v1/shipments",
            new CreateShipmentRequest(
                orderId,
                shippingQuoteId,
                new ShipmentRecipientRequest(
                    recipient.CustomerId,
                    recipient.FullName,
                    recipient.AddressLine1,
                    recipient.AddressLine2,
                    recipient.City,
                    recipient.StateOrProvince,
                    recipient.PostalCode,
                    recipient.Country)),
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

    public Task<ShipmentSnapshot> MarkInTransitAsync(
        Guid shipmentId,
        CancellationToken cancellationToken) =>
        MarkShipmentAsync($"api/v1/shipments/{shipmentId}/in-transit", cancellationToken);

    public Task<ShipmentSnapshot> MarkDeliveredAsync(
        Guid shipmentId,
        CancellationToken cancellationToken) =>
        MarkShipmentAsync($"api/v1/shipments/{shipmentId}/delivered", cancellationToken);

    private async Task<ShipmentSnapshot> MarkShipmentAsync(string endpoint, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync(endpoint, null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ShippingError>(cancellationToken: cancellationToken);
            throw new InvalidOperationException(error?.Message ?? "Shipping rejected the shipment transition.");
        }

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

    private sealed record ShippingError(string Code, string Message);
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
