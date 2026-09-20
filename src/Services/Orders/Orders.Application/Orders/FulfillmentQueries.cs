using Luna.Orders.Contracts.Orders;

namespace Luna.Orders.Application.Orders;

public sealed record FulfillmentQueueQuery(
    string? Status,
    string? Search,
    int Page = 1,
    int PageSize = 25);

public sealed class GetFulfillmentQueueHandler(IOrderReadRepository repository)
{
    public Task<FulfillmentQueueResponse> HandleAsync(
        FulfillmentQueueQuery query,
        CancellationToken cancellationToken) =>
        repository.GetFulfillmentQueueAsync(query, cancellationToken);
}

public sealed class GetFulfillmentOrderHandler(IOrderReadRepository repository)
{
    public Task<FulfillmentOrderResponse?> HandleAsync(
        Guid orderId,
        CancellationToken cancellationToken) =>
        repository.GetFulfillmentOrderAsync(orderId, cancellationToken);
}
