using Luna.Orders.Contracts.Checkout;
using Luna.Orders.Contracts.Carts;
using Luna.Orders.Domain;
using Luna.Orders.Application.Carts;
using Luna.Orders.Application.Orders;
using Microsoft.Extensions.Logging;

namespace Luna.Orders.Application.Checkout;

public sealed record CheckoutCommand(Guid CustomerId, CheckoutRequest Request, string IdempotencyKey);

public sealed class CheckoutHandler(
    ICartReadRepository cartRepository,
    IOrderWriteRepository orderRepository,
    ICatalogCheckoutClient catalogClient,
    IShippingCheckoutClient shippingClient,
    IInventoryCheckoutClient inventoryClient,
    IPaymentsCheckoutClient paymentsClient,
    ILogger<CheckoutHandler> logger)
{
    public async Task<CheckoutResponse> HandleAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCoreAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Checkout failed for customer {CustomerId}", command.CustomerId);
            throw;
        }
    }

    private async Task<CheckoutResponse> HandleCoreAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        ValidateCommand(command);
        logger.LogInformation("Checkout started for customer {CustomerId}", command.CustomerId);

        var idempotencyKey = NormalizeIdempotencyKey(command.IdempotencyKey);
        var existingCheckout = await TryGetExistingCheckoutAsync(command.CustomerId, idempotencyKey, command.Request, cancellationToken);
        if (existingCheckout is not null)
        {
            return existingCheckout;
        }

        ValidatePayment(command.Request);
        var cart = await LoadCartAsync(command.CustomerId, cancellationToken);
        logger.LogInformation("Cart loaded for customer {CustomerId} with {ItemCount} items", command.CustomerId, cart.Items.Count);
        var address = CreateShippingAddress(command.Request);
        var products = await LoadProductsAsync(cart, cancellationToken);
        logger.LogInformation("Products loaded for checkout with {ProductCount} distinct products", products.Count);
        var orderId = Guid.NewGuid();
        var quote = await shippingClient.QuoteAsync(orderId, command.Request.ShippingMethodCode, address, cancellationToken);
        logger.LogInformation("Shipping quote obtained for order {OrderId} using method {ShippingMethodCode}", orderId, quote.ShippingMethodCode);
        var order = CreateOrder(command, cart, products, address, quote, idempotencyKey, orderId);
        logger.LogInformation("Order created for checkout {OrderId} with total {OrderTotal}", order.Id, order.Total);

        await orderRepository.AddAsync(order, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        var reservation = await ReserveInventoryAsync(order, cart, cancellationToken);
        var payment = await AuthorizePaymentAsync(order, reservation, command.Request, cancellationToken);
        await ConfirmOrderAsync(order, reservation, payment, cancellationToken);
        logger.LogInformation("Order confirmed for checkout {OrderId}", order.Id);

        return CreateResponse(order, reservation, payment, command.Request);
    }

    private static void ValidateCommand(CheckoutCommand command)
    {
        if (command.CustomerId == Guid.Empty)
        {
            throw new CheckoutRejectedException("INVALID_CUSTOMER", "Customer ID is required.");
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new CheckoutRejectedException("INVALID_IDEMPOTENCY_KEY", "An Idempotency-Key header is required.");
        }
    }

    private static string NormalizeIdempotencyKey(string key)
    {
        var normalized = key.Trim();
        if (normalized.Length > 200)
        {
            throw new CheckoutRejectedException("INVALID_IDEMPOTENCY_KEY", "The Idempotency-Key header is too long.");
        }

        return normalized;
    }

    private async Task<CheckoutResponse?> TryGetExistingCheckoutAsync(
        Guid customerId,
        string idempotencyKey,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var existingOrder = await orderRepository.GetByIdempotencyKeyAsync(customerId, idempotencyKey, cancellationToken);
        if (existingOrder is null)
        {
            return null;
        }

        if (existingOrder.Status == OrderStatus.Confirmed
            && existingOrder.InventoryReservationId is Guid reservationId
            && existingOrder.PaymentId is Guid paymentId)
        {
            logger.LogInformation("Existing idempotent checkout detected for customer {CustomerId} and order {OrderId}", customerId, existingOrder.Id);
            return new CheckoutResponse(existingOrder.Id, existingOrder.Status.ToString(), existingOrder.Total, request.Currency.Trim().ToUpperInvariant(), reservationId, paymentId);
        }

        throw new CheckoutRejectedException("CHECKOUT_IN_PROGRESS", "This checkout attempt is already being processed.");
    }

    private async Task<CartResponse> LoadCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetCartAsync(customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            throw new CheckoutRejectedException("EMPTY_CART", "The cart must contain at least one item.");
        }

        return cart;
    }

    private static ShippingAddress CreateShippingAddress(CheckoutRequest request) =>
        ShippingAddress.Create(
            request.FullName,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.Country);

    private async Task<IReadOnlyDictionary<Guid, CatalogProductSnapshot>> LoadProductsAsync(
        CartResponse cart,
        CancellationToken cancellationToken)
    {
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

        return products;
    }

    private static Order CreateOrder(
        CheckoutCommand command,
        CartResponse cart,
        IReadOnlyDictionary<Guid, CatalogProductSnapshot> products,
        ShippingAddress address,
        ShippingQuoteSnapshot quote,
        string idempotencyKey,
        Guid orderId)
    {
        return Order.Create(
            command.CustomerId,
            cart.Items.Select(item =>
            {
                var product = products[item.ProductId];
                return new OrderItemSnapshot(item.ProductId, product.Sku, product.Name, product.CurrentPrice, item.Quantity);
            }).ToArray(),
            address,
            quote.ShippingMethodCode,
            quote.Cost,
            idempotencyKey,
            orderId,
            quote.QuoteId);
    }

    private async Task<InventoryReservationSnapshot> ReserveInventoryAsync(
        Order order,
        CartResponse cart,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Inventory reservation attempted for order {OrderId}", order.Id);
            return await inventoryClient.ReserveAsync(
                order.Id,
                cart.Items.Select(item => (item.ProductId, item.Quantity)).ToArray(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Inventory reservation failed for order {OrderId}", order.Id);
            order.Cancel();
            await orderRepository.SaveChangesAsync(cancellationToken);
            throw new CheckoutRejectedException("INVENTORY_UNAVAILABLE", "The requested inventory is not available.", exception);
        }
    }

    private async Task<PaymentAuthorizationSnapshot> AuthorizePaymentAsync(
        Order order,
        InventoryReservationSnapshot reservation,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        PaymentAuthorizationSnapshot payment;
        try
        {
            logger.LogInformation("Payment authorization attempted for order {OrderId}", order.Id);
            payment = await paymentsClient.AuthorizeAsync(order.Id, order.Total, request.Currency, request.PaymentMethod, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Payment authorization failed for order {OrderId}", order.Id);
            await HandlePaymentFailureAsync(order, reservation, "PAYMENT_UNAVAILABLE", "Payment authorization could not be completed.", exception, cancellationToken);
            throw new InvalidOperationException("Payment failure handling must not return.");
        }

        if (!payment.Authorized)
        {
            logger.LogWarning("Payment authorization declined for order {OrderId}", order.Id);
            await HandlePaymentFailureAsync(order, reservation, "PAYMENT_DECLINED", "Payment authorization was declined.", null, cancellationToken);
            throw new InvalidOperationException("Payment failure handling must not return.");
        }

        logger.LogInformation("Payment authorization completed for order {OrderId}", order.Id);
        return payment;
    }

    private async Task HandlePaymentFailureAsync(
        Order order,
        InventoryReservationSnapshot reservation,
        string code,
        string message,
        Exception? innerException,
        CancellationToken cancellationToken)
    {
        var releaseSucceeded = await TryReleaseReservationAsync(reservation.ReservationId, cancellationToken);
        order.MarkPaymentFailed();
        await orderRepository.SaveChangesAsync(cancellationToken);

        if (!releaseSucceeded)
        {
            throw new CheckoutRejectedException("RESERVATION_RELEASE_FAILED", "Payment failed and the inventory reservation could not be released.", innerException);
        }

        throw new CheckoutRejectedException(code, message, innerException);
    }

    private async Task ConfirmOrderAsync(
        Order order,
        InventoryReservationSnapshot reservation,
        PaymentAuthorizationSnapshot payment,
        CancellationToken cancellationToken)
    {
        order.RecordCheckoutResult(reservation.ReservationId, payment.PaymentId);
        order.Confirm();
        await orderRepository.SaveChangesAsync(cancellationToken);
    }

    private static CheckoutResponse CreateResponse(
        Order order,
        InventoryReservationSnapshot reservation,
        PaymentAuthorizationSnapshot payment,
        CheckoutRequest request) =>
        new(order.Id, order.Status.ToString(), order.Total, request.Currency.Trim().ToUpperInvariant(), reservation.ReservationId, payment.PaymentId);

    private async Task<bool> TryReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        try
        {
            await inventoryClient.ReleaseAsync(reservationId, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Inventory reservation release failed for reservation {ReservationId}", reservationId);
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
