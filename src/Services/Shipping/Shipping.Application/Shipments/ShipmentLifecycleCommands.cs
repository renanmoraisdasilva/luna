using Luna.Shipping.Contracts;

namespace Luna.Shipping.Application.Shipments;

public sealed record MarkShipmentInTransitCommand(Guid ShipmentId);

public sealed class MarkShipmentInTransitHandler(IShipmentWriteRepository repository)
{
    public async Task<ShipmentResponse?> HandleAsync(
        MarkShipmentInTransitCommand command,
        CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        shipment.MarkInTransit();
        await repository.SaveChangesAsync(cancellationToken);
        return ShipmentResponseMapper.Map(shipment);
    }
}

public sealed record MarkShipmentDeliveredCommand(Guid ShipmentId);

public sealed class MarkShipmentDeliveredHandler(IShipmentWriteRepository repository)
{
    public async Task<ShipmentResponse?> HandleAsync(
        MarkShipmentDeliveredCommand command,
        CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return null;
        }

        shipment.MarkDelivered();
        await repository.SaveChangesAsync(cancellationToken);
        return ShipmentResponseMapper.Map(shipment);
    }
}