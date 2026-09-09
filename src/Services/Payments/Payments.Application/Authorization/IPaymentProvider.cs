namespace Luna.Payments.Application.Authorization;

public sealed record PaymentProviderAuthorization(
    bool Succeeded,
    string ProviderReference,
    string? FailureCode);

public interface IPaymentProvider
{
    Task<PaymentProviderAuthorization> AuthorizeAsync(
        Guid orderId,
        decimal amount,
        string currency,
        string paymentMethod,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
