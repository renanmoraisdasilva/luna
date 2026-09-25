using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;

namespace Luna.Shipping.Application.Shipments;

public sealed record CreateShipmentCommand(Guid OrderId, Guid QuoteId);

public sealed class CreateShipmentHandler(
    IShippingQuoteReadRepository quoteRepository,
    IShipmentWriteRepository shipmentRepository)
{
    public async Task<ShipmentResponse> HandleAsync(
        CreateShipmentCommand command,
        CancellationToken cancellationToken)
    {
        if (await shipmentRepository.ExistsByOrderIdAsync(command.OrderId, cancellationToken))
        {
            throw new InvalidOperationException("A shipment already exists for this order.");
        }

        var quote = await quoteRepository.GetQuoteAsync(command.QuoteId, cancellationToken);
        if (quote.OrderId != command.OrderId)
        {
            throw new InvalidOperationException("The shipping quote does not belong to the order.");
        }

        var shipment = Shipment.Create(command.OrderId, command.QuoteId, $"LUNA-{Guid.NewGuid():N}"[..18].ToUpperInvariant());
        await shipmentRepository.AddAsync(shipment, cancellationToken);

        return new ShipmentResponse(
            shipment.Id,
            shipment.OrderId,
            shipment.QuoteId,
            shipment.Status.ToString(),
            shipment.TrackingNumber,
            shipment.DeliveredAt);
    }
}
