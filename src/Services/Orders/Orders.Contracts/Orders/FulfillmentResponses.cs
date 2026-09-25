namespace Luna.Orders.Contracts.Orders;

/// <summary>Returns a paged Operations view of orders requiring fulfillment action.</summary>
public sealed record FulfillmentQueueResponse(
    IReadOnlyCollection<FulfillmentOrderSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Identifies an order and the fulfillment action currently available to Operations.</summary>
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

/// <summary>Returns the order information required by the Operations fulfillment detail view.</summary>
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

/// <summary>Returns the state produced by an Operations fulfillment command.</summary>
public sealed record FulfillmentCommandResponse(
    Guid OrderId,
    string OrderStatus);
