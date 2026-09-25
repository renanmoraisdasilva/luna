using Luna.Orders.Contracts.Orders;

namespace Luna.Orders.Application.Orders;

public sealed record PrepareFulfillmentOrderCommand(Guid OrderId);

public sealed class PrepareFulfillmentOrderHandler(IOrderWriteRepository repository)
{
    public async Task<FulfillmentCommandResponse?> HandleAsync(
        PrepareFulfillmentOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.Prepare();
        await repository.SaveChangesAsync(cancellationToken);

        return new FulfillmentCommandResponse(order.Id, order.Status.ToString());
    }
}
