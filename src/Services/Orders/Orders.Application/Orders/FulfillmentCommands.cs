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

        var shipment = await shippingClient.CreateShipmentAsync(
            order.Id,
            shippingQuoteId,
            new ShipmentRecipientSnapshot(
                order.CustomerId,
                order.ShippingAddress.FullName,
                order.ShippingAddress.AddressLine1,
                order.ShippingAddress.AddressLine2,
                order.ShippingAddress.City,
                order.ShippingAddress.StateOrProvince,
                order.ShippingAddress.PostalCode,
                order.ShippingAddress.Country),
            cancellationToken);
        order.RecordShipment(shipment.ShipmentId);
        order.MarkShipped();
        await repository.SaveChangesAsync(cancellationToken);

        return new FulfillmentCommandResponse(order.Id, order.Status.ToString());
    }
}

public sealed record MarkShipmentInTransitCommand(Guid ShipmentId);

public sealed class MarkShipmentInTransitHandler(
    IOrderWriteRepository repository,
    IShippingFulfillmentClient shippingClient)
{
    public async Task<FulfillmentCommandResponse?> HandleAsync(
        MarkShipmentInTransitCommand command,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByShipmentIdAsync(command.ShipmentId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.EnsureStatusForShipmentInTransit();
        await shippingClient.MarkInTransitAsync(command.ShipmentId, cancellationToken);
        return new FulfillmentCommandResponse(order.Id, order.Status.ToString());
    }
}

public sealed record MarkShipmentDeliveredCommand(Guid ShipmentId);

public sealed class MarkShipmentDeliveredHandler(
    IOrderWriteRepository repository,
    IShippingFulfillmentClient shippingClient)
{
    public async Task<FulfillmentCommandResponse?> HandleAsync(
        MarkShipmentDeliveredCommand command,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByShipmentIdAsync(command.ShipmentId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.EnsureDeliveryAllowed();
        await shippingClient.MarkDeliveredAsync(command.ShipmentId, cancellationToken);
        order.MarkDelivered();
        await repository.SaveChangesAsync(cancellationToken);
        return new FulfillmentCommandResponse(order.Id, order.Status.ToString());
    }
}
