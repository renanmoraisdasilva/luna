using Luna.Orders.Application.Orders;
using Luna.Orders.Contracts.Orders;
using Luna.Orders.Domain;
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
                order.Items.Sum(item => item.LineTotal) + order.ShippingCost,
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
                order.ShipmentId,
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

            public async Task<FulfillmentQueueResponse> GetFulfillmentQueueAsync(
                FulfillmentQueueQuery query,
                CancellationToken cancellationToken)
            {
                var statuses = ParseStatuses(query.Status);
                ValidatePaging(query.Page, query.PageSize);

                var ordersQuery = dbContext.Orders
                    .AsNoTracking()
                    .Where(order => statuses.Contains(order.Status));

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var search = query.Search.Trim();
                    var hasOrderId = Guid.TryParse(search, out var orderId);
                    var hasCustomerId = Guid.TryParse(search, out var customerId);

                    ordersQuery = ordersQuery.Where(order =>
                        (hasOrderId && order.Id == orderId)
                        || (hasCustomerId && order.CustomerId == customerId)
                        || order.ShippingAddress.FullName.Contains(search)
                        || order.Items.Any(item => item.Sku.Contains(search) || item.ProductName.Contains(search)));
                }

                var totalCount = await ordersQuery.CountAsync(cancellationToken);
                var items = await ordersQuery
                    .OrderByDescending(order => order.CreatedAt)
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .Select(order => new
                    {
                        order.Id,
                        order.CustomerId,
                        CustomerName = order.ShippingAddress.FullName,
                        ItemCount = order.Items.Count,
                        Total = order.Items.Sum(item => item.LineTotal) + order.ShippingCost,
                        PaymentStatus = order.PaymentId.HasValue ? "Authorized" : "NotAuthorized",
                        order.Status,
                        order.CreatedAt,
                    })
                    .ToArrayAsync(cancellationToken);

                return new FulfillmentQueueResponse(
                    items.Select(order => new FulfillmentOrderSummaryResponse(
                        order.Id,
                        order.CustomerId,
                        order.CustomerName,
                        order.ItemCount,
                        order.Total,
                        order.PaymentStatus,
                        order.Status.ToString(),
                        order.CreatedAt,
                        GetAvailableAction(order.Status))).ToArray(),
                    totalCount,
                    query.Page,
                    query.PageSize);
            }

            public async Task<FulfillmentOrderResponse?> GetFulfillmentOrderAsync(
                Guid orderId,
                CancellationToken cancellationToken)
            {
                var order = await dbContext.Orders
                    .AsNoTracking()
                    .Where(order => order.Id == orderId)
                    .Select(order => new
                    {
                        order.Id,
                        order.CustomerId,
                        order.Status,
                        PaymentStatus = order.PaymentId.HasValue ? "Authorized" : "NotAuthorized",
                        Subtotal = order.Items.Sum(item => item.LineTotal),
                        order.ShippingCost,
                        Total = order.Items.Sum(item => item.LineTotal) + order.ShippingCost,
                        order.ShippingMethodCode,
                        order.CreatedAt,
                        ShippingAddress = new ShippingAddressResponse(
                            order.ShippingAddress.FullName,
                            order.ShippingAddress.AddressLine1,
                            order.ShippingAddress.AddressLine2,
                            order.ShippingAddress.City,
                            order.ShippingAddress.StateOrProvince,
                            order.ShippingAddress.PostalCode,
                            order.ShippingAddress.Country),
                        Items = order.Items
                            .OrderBy(item => item.Id)
                            .Select(item => new OrderItemResponse(
                                item.ProductId,
                                item.Sku,
                                item.ProductName,
                                item.UnitPrice,
                                item.Quantity,
                                item.LineTotal))
                            .ToArray(),
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                return order is null
                    ? null
                    : new FulfillmentOrderResponse(
                        order.Id,
                        order.CustomerId,
                        order.ShippingAddress.FullName,
                        order.Status.ToString(),
                        order.PaymentStatus,
                        order.Subtotal,
                        order.ShippingCost,
                        order.Total,
                        order.ShippingMethodCode,
                        order.CreatedAt,
                        order.ShippingAddress,
                        order.Items,
                        GetAvailableAction(order.Status));
            }

            private static IReadOnlyCollection<OrderStatus> ParseStatuses(string? status)
            {
                if (string.IsNullOrWhiteSpace(status))
                {
                    return [OrderStatus.Confirmed, OrderStatus.Preparing];
                }

                var parsedStatuses = status
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var parsed)
                        ? parsed
                        : throw new ArgumentException($"Unsupported fulfillment status '{value}'.", nameof(status)))
                    .Distinct()
                    .ToArray();

                if (parsedStatuses.Any(statusValue => statusValue is not (OrderStatus.Confirmed or OrderStatus.Preparing)))
                {
                    throw new ArgumentException("Fulfillment status must be Confirmed or Preparing.", nameof(status));
                }

                return parsedStatuses;
            }

            private static void ValidatePaging(int page, int pageSize)
            {
                if (page < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
                }

                if (pageSize is < 1 or > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
                }
            }

            private static string GetAvailableAction(OrderStatus status) => status switch
            {
                OrderStatus.Confirmed => "StartPreparing",
                OrderStatus.Preparing => "CreateShipment",
                _ => "None",
            };
}
