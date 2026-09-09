using Luna.Orders.Contracts.Checkout;
using Luna.Orders.Domain;
using Luna.Orders.Application.Carts;

namespace Luna.Orders.Application.Checkout;

public sealed record CheckoutCommand(Guid CustomerId, CheckoutRequest Request);

public sealed class CheckoutHandler(
    ICartReadRepository cartRepository,
    IOrderWriteRepository orderRepository,
    ICatalogCheckoutClient catalogClient,
    IShippingCheckoutClient shippingClient,
    IInventoryCheckoutClient inventoryClient,
    IPaymentsCheckoutClient paymentsClient)
{
    public async Task<CheckoutResponse> HandleAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            throw new CheckoutRejectedException("INVALID_CUSTOMER", "Customer ID is required.");
        }

        var request = command.Request;
        var cart = await cartRepository.GetCartAsync(command.CustomerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            throw new CheckoutRejectedException("EMPTY_CART", "The cart must contain at least one item.");
        }

        var address = ShippingAddress.Create(
            request.FullName,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.Country);
        ValidatePayment(request);

        var products = new Dictionary<Guid, CatalogProductSnapshot>();
        foreach (var item in cart.Items)
        {
            if (products.ContainsKey(item.ProductId))
            {
                continue;
            }

            var product = await catalogClient.GetProductAsync(item.ProductId, cancellationToken);
            if (product is null)
            {
                throw new CheckoutRejectedException("PRODUCT_UNAVAILABLE", $"Product {item.ProductId} is no longer available.");
            }

            products.Add(item.ProductId, product);
        }

        var orderId = Guid.NewGuid();
        var quote = await shippingClient.QuoteAsync(orderId, request.ShippingMethodCode, address, cancellationToken);
        var order = Order.Create(
            command.CustomerId,
            cart.Items.Select(item =>
            {
                var product = products[item.ProductId];
                return new OrderItemSnapshot(item.ProductId, product.Sku, product.Name, product.CurrentPrice, item.Quantity);
            }).ToArray(),
            address,
            quote.ShippingMethodCode,
            quote.Cost,
            orderId);

        await orderRepository.AddAsync(order, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        InventoryReservationSnapshot? reservation = null;
        try
        {
            reservation = await inventoryClient.ReserveAsync(
                order.Id,
                cart.Items.Select(item => (item.ProductId, item.Quantity)).ToArray(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            order.Cancel();
            await orderRepository.SaveChangesAsync(cancellationToken);
            throw new CheckoutRejectedException("INVENTORY_UNAVAILABLE", "The requested inventory is not available.");
        }

        PaymentAuthorizationSnapshot payment;
        try
        {
            payment = await paymentsClient.AuthorizeAsync(order.Id, order.Total, request.Currency, request.PaymentMethod, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var releaseSucceeded = await TryReleaseReservationAsync(reservation.ReservationId, cancellationToken);
            order.MarkPaymentFailed();
            await orderRepository.SaveChangesAsync(cancellationToken);
            if (!releaseSucceeded)
            {
                throw new CheckoutRejectedException("RESERVATION_RELEASE_FAILED", "Payment failed and the inventory reservation could not be released.");
            }

            throw new CheckoutRejectedException("PAYMENT_UNAVAILABLE", "Payment authorization could not be completed.");
        }

        if (!payment.Authorized)
        {
            var releaseSucceeded = await TryReleaseReservationAsync(reservation.ReservationId, cancellationToken);
            order.MarkPaymentFailed();
            await orderRepository.SaveChangesAsync(cancellationToken);
            if (!releaseSucceeded)
            {
                throw new CheckoutRejectedException("RESERVATION_RELEASE_FAILED", "Payment failed and the inventory reservation could not be released.");
            }

            throw new CheckoutRejectedException("PAYMENT_DECLINED", "Payment authorization was declined.");
        }

        order.Confirm();
        await orderRepository.SaveChangesAsync(cancellationToken);
        return new CheckoutResponse(order.Id, order.Status.ToString(), order.Total, request.Currency.Trim().ToUpperInvariant(), reservation.ReservationId, payment.PaymentId);
    }

    private async Task<bool> TryReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        try
        {
            await inventoryClient.ReleaseAsync(reservationId, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    private static void ValidatePayment(CheckoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            throw new CheckoutRejectedException("INVALID_PAYMENT_METHOD", "Payment method is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            throw new CheckoutRejectedException("INVALID_CURRENCY", "Currency must be a three-letter code.");
        }
    }
}
