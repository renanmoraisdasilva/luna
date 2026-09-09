using Luna.Shipping.Contracts;
namespace Luna.Shipping.Application.ShippingMethods;

public sealed class GetShippingMethodsHandler(IShippingMethodReadRepository repository)
{
    public async Task<GetShippingMethodsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var methods = await repository.GetAllAsync(cancellationToken);
        return new GetShippingMethodsResponse(methods
            .Select(method => new ShippingMethodResponse(
                method.Id,
                method.Code,
                method.Name,
                method.Cost,
                method.EstimatedDeliveryDays))
            .ToArray());
    }
}
