namespace Luna.Payments.Domain;

public sealed class Payment
{
    private readonly List<PaymentAttempt> attempts = [];

    private Payment()
    {
    }

    private Payment(Guid id, Guid orderId, decimal amount, string currency)
    {
        Id = id;
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<PaymentAttempt> Attempts => attempts.AsReadOnly();

    public static Payment Create(Guid orderId, decimal amount, string currency, Guid? id = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a three-letter code.", nameof(currency));
        }

        return new Payment(id ?? Guid.NewGuid(), orderId, decimal.Round(amount, 2), currency.Trim().ToUpperInvariant());
    }

    public PaymentAttempt RecordAuthorizationAttempt(
        bool succeeded,
        string providerReference,
        string? failureCode = null,
        DateTimeOffset? attemptedAt = null,
        Guid? id = null)
    {
        if (Status == PaymentStatus.Authorized)
        {
            throw new InvalidOperationException("An authorized payment cannot be authorized again.");
        }

        if (string.IsNullOrWhiteSpace(providerReference))
        {
            throw new ArgumentException("Provider reference is required.", nameof(providerReference));
        }

        var attempt = PaymentAttempt.Create(
            id ?? Guid.NewGuid(),
            Id,
            Amount,
            succeeded,
            providerReference,
            failureCode,
            attemptedAt ?? DateTimeOffset.UtcNow);
        attempts.Add(attempt);
        Status = succeeded ? PaymentStatus.Authorized : PaymentStatus.Failed;
        return attempt;
    }
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Failed
}
