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

public sealed record CreateShipmentCommand(Guid OrderId);

public sealed class CreateShipmentHandler(
    IOrderWriteRepository repository,
    IShippingFulfillmentClient shippingClient)
{
    public async Task<FulfillmentCommandResponse?> HandleAsync(
        CreateShipmentCommand command,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.EnsureShipmentCreationAllowed();
        if (order.ShippingQuoteId is not Guid shippingQuoteId)
        {
            throw new InvalidOperationException("The order does not have a shipping quote.");
        }

        try
        {
            await shippingClient.CreateShipmentAsync(order.Id, shippingQuoteId, cancellationToken);
            order.MarkShipped();
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            order.MarkShippingPendingRetry();
            await repository.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new FulfillmentCommandResponse(order.Id, order.Status.ToString());
    }
}
