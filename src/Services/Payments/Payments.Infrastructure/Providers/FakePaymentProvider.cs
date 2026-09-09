using Luna.Payments.Application.Authorization;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace Luna.Payments.Infrastructure.Providers;

public sealed class FakePaymentProvider(IConfiguration configuration) : IPaymentProvider
{
    private const string DefaultDeclinedPaymentMethod = "declined";
    private readonly ConcurrentDictionary<string, PaymentProviderAuthorization> authorizations = new(StringComparer.Ordinal);

    public Task<PaymentProviderAuthorization> AuthorizeAsync(
        Guid orderId,
        decimal amount,
        string currency,
        string paymentMethod,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (authorizations.TryGetValue(idempotencyKey, out var previousAuthorization))
        {
            return Task.FromResult(previousAuthorization);
        }

        var declinedMethods = configuration
            .GetSection("PaymentProvider:DeclinedPaymentMethods")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (declinedMethods.Count == 0)
        {
            declinedMethods.Add(DefaultDeclinedPaymentMethod);
        }

        var declined = declinedMethods.Contains(paymentMethod.Trim());
        var authorization = declined
            ? new PaymentProviderAuthorization(false, $"fake-{orderId:N}", "PAYMENT_DECLINED")
            : new PaymentProviderAuthorization(true, $"fake-{orderId:N}", null);
        return Task.FromResult(authorizations.GetOrAdd(idempotencyKey, authorization));
    }
}
