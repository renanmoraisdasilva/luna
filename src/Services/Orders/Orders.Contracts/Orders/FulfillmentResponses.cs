namespace Luna.Orders.Contracts.Orders;

public sealed record FulfillmentQueueResponse(
    IReadOnlyCollection<FulfillmentOrderSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record FulfillmentOrderSummaryResponse(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    int ItemCount,
    decimal Total,
    string PaymentStatus,
    string OrderStatus,
    DateTimeOffset CreatedAt,
    string AvailableAction);

public sealed record FulfillmentOrderResponse(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    string OrderStatus,
    string PaymentStatus,
    decimal Subtotal,
    decimal ShippingCost,
    decimal Total,
    string ShippingMethodCode,
    DateTimeOffset CreatedAt,
    ShippingAddressResponse ShippingAddress,
    IReadOnlyCollection<OrderItemResponse> Items,
    string AvailableAction);

public sealed record FulfillmentCommandResponse(
    Guid OrderId,
    string OrderStatus);
