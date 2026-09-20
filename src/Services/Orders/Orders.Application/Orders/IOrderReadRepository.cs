using Luna.Orders.Contracts.Orders;

namespace Luna.Orders.Application.Orders;

public interface IOrderReadRepository
{
    Task<IReadOnlyCollection<OrderSummaryResponse>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<OrderResponse?> GetByIdAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
    Task<FulfillmentQueueResponse> GetFulfillmentQueueAsync(FulfillmentQueueQuery query, CancellationToken cancellationToken);
    Task<FulfillmentOrderResponse?> GetFulfillmentOrderAsync(Guid orderId, CancellationToken cancellationToken);
}
