using Luna.Orders.Application.Orders;
using Luna.Orders.Contracts.Orders;
using Luna.Orders.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Luna.Orders.Infrastructure.Repositories;

public sealed class OrderReadRepository(OrdersDbContext dbContext) : IOrderReadRepository
{
    public async Task<IReadOnlyCollection<OrderSummaryResponse>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderSummaryResponse(
                order.Id,
                order.Status.ToString(),
                order.Total,
                order.CreatedAt,
                order.Items.Count))
            .ToArrayAsync(cancellationToken);

    public Task<OrderResponse?> GetByIdAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId && order.Id == orderId)
            .Select(order => new OrderResponse(
                order.Id,
                order.CustomerId,
                order.Status.ToString(),
                order.Items.Sum(item => item.LineTotal),
                order.ShippingCost,
                order.Items.Sum(item => item.LineTotal) + order.ShippingCost,
                order.ShippingMethodCode,
                order.CreatedAt,
                new ShippingAddressResponse(
                    order.ShippingAddress.FullName,
                    order.ShippingAddress.AddressLine1,
                    order.ShippingAddress.AddressLine2,
                    order.ShippingAddress.City,
                    order.ShippingAddress.StateOrProvince,
                    order.ShippingAddress.PostalCode,
                    order.ShippingAddress.Country),
                order.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemResponse(
                        item.ProductId,
                        item.Sku,
                        item.ProductName,
                        item.UnitPrice,
                        item.Quantity,
                        item.LineTotal))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
}
