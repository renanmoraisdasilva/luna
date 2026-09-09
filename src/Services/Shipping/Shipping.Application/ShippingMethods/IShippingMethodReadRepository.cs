namespace Luna.Shipping.Application.ShippingMethods;

public sealed record ShippingMethodReadModel(
    Guid Id,
    string Code,
    string Name,
    decimal Cost,
    int EstimatedDeliveryDays);

public interface IShippingMethodReadRepository
{
    Task<IReadOnlyCollection<ShippingMethodReadModel>> GetAllAsync(CancellationToken cancellationToken);
    Task<ShippingMethodReadModel> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
