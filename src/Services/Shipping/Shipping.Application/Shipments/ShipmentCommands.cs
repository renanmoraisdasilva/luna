using Luna.Shipping.Contracts;
using Luna.Shipping.Domain;

namespace Luna.Shipping.Application.Shipments;

public sealed record ShipmentRecipientCommand(
    Guid CustomerId,
    string FullName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);

public sealed record CreateShipmentCommand(Guid OrderId, Guid QuoteId, ShipmentRecipientCommand Recipient);

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

        var recipient = ShipmentRecipientSnapshot.Create(
            command.Recipient.CustomerId,
            command.Recipient.FullName,
            command.Recipient.AddressLine1,
            command.Recipient.AddressLine2,
            command.Recipient.City,
            command.Recipient.StateOrProvince,
            command.Recipient.PostalCode,
            command.Recipient.Country);
        var shipment = Shipment.Create(command.OrderId, command.QuoteId, $"LUNA-{Guid.NewGuid():N}"[..18].ToUpperInvariant(), recipient);
        await shipmentRepository.AddAsync(shipment, cancellationToken);

        return new ShipmentResponse(
            shipment.Id,
            shipment.OrderId,
            shipment.QuoteId,
            shipment.Status.ToString(),
            shipment.TrackingNumber,
            shipment.DeliveredAt,
            new ShipmentRecipientResponse(
                shipment.Recipient.CustomerId,
                shipment.Recipient.FullName,
                shipment.Recipient.AddressLine1,
                shipment.Recipient.AddressLine2,
                shipment.Recipient.City,
                shipment.Recipient.StateOrProvince,
                shipment.Recipient.PostalCode,
                shipment.Recipient.Country));
    }
}
