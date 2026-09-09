using Luna.Payments.Contracts;
using Luna.Payments.Domain;

namespace Luna.Payments.Application.Authorization;

public sealed record AuthorizePaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public sealed class AuthorizePaymentHandler(
    IPaymentWriteRepository paymentRepository,
    IPaymentUnitOfWork unitOfWork,
    IPaymentProvider paymentProvider)
{
    public async Task<AuthorizePaymentResponse> HandleAsync(
        AuthorizePaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(command));
        }

        if (command.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.Amount), "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
        {
            throw new ArgumentException("Payment method is required.", nameof(command));
        }

        var payment = await paymentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (payment is null)
        {
            payment = Payment.Create(command.OrderId, command.Amount, command.Currency);
            await paymentRepository.AddAsync(payment, cancellationToken);
        }
        else if (payment.Amount != decimal.Round(command.Amount, 2)
            || !string.Equals(payment.Currency, command.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The payment request does not match the existing order payment.");
        }

        var providerResult = await paymentProvider.AuthorizeAsync(
            command.OrderId,
            payment.Amount,
            payment.Currency,
            command.PaymentMethod,
            $"payment-authorization:{command.OrderId:N}",
            cancellationToken);
        var attempt = payment.RecordAuthorizationAttempt(
            providerResult.Succeeded,
            providerResult.ProviderReference,
            providerResult.FailureCode);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthorizePaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            attempt.Id,
            providerResult.Succeeded,
            attempt.FailureCode,
            attempt.AttemptedAt);
    }
}
