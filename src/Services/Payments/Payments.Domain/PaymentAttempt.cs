namespace Luna.Payments.Domain;

public sealed class PaymentAttempt
{
    private PaymentAttempt()
    {
    }

    private PaymentAttempt(
        Guid id,
        Guid paymentId,
        decimal amount,
        bool succeeded,
        string providerReference,
        string? failureCode,
        DateTimeOffset attemptedAt)
    {
        Id = id;
        PaymentId = paymentId;
        Amount = amount;
        Succeeded = succeeded;
        ProviderReference = providerReference;
        FailureCode = failureCode;
        AttemptedAt = attemptedAt;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public bool Succeeded { get; private set; }
    public string ProviderReference { get; private set; } = string.Empty;
    public string? FailureCode { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }

    internal static PaymentAttempt Create(
        Guid id,
        Guid paymentId,
        decimal amount,
        bool succeeded,
        string providerReference,
        string? failureCode,
        DateTimeOffset attemptedAt) =>
        new(id, paymentId, amount, succeeded, providerReference.Trim(), failureCode?.Trim(), attemptedAt);
}
