using Luna.Orders.Contracts.Orders;

namespace Luna.Orders.Application.Orders;

public interface IOrderReadRepository
{
    Task<IReadOnlyCollection<OrderSummaryResponse>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<OrderResponse?> GetByIdAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken);
}
