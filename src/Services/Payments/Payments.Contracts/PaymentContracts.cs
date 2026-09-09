namespace Luna.Payments.Contracts;

public sealed record AuthorizePaymentRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public sealed record AuthorizePaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    Guid AttemptId,
    bool Authorized,
    string? FailureCode,
    DateTimeOffset AttemptedAt);
