using FluentAssertions;
using Luna.Payments.Domain;
using Xunit;

namespace Luna.UnitTests.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void Payment_records_a_successful_authorization_attempt()
    {
        var payment = Payment.Create(Guid.NewGuid(), 42.50m, "usd");

        var attempt = payment.RecordAuthorizationAttempt(true, "fake-reference");

        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.Currency.Should().Be("USD");
        payment.Attempts.Should().ContainSingle().Which.Should().Be(attempt);
        attempt.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Payment_records_a_failed_authorization_attempt()
    {
        var payment = Payment.Create(Guid.NewGuid(), 42.50m, "USD");

        var attempt = payment.RecordAuthorizationAttempt(false, "fake-reference", "PAYMENT_DECLINED");

        payment.Status.Should().Be(PaymentStatus.Failed);
        attempt.FailureCode.Should().Be("PAYMENT_DECLINED");
    }

    [Fact]
    public void Authorized_payment_cannot_be_authorized_again()
    {
        var payment = Payment.Create(Guid.NewGuid(), 42.50m, "USD");
        payment.RecordAuthorizationAttempt(true, "fake-reference");

        var act = () => payment.RecordAuthorizationAttempt(true, "another-reference");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Payment_requires_positive_amount_and_three_letter_currency()
    {
        var amountAct = () => Payment.Create(Guid.NewGuid(), 0, "USD");
        var currencyAct = () => Payment.Create(Guid.NewGuid(), 1, "US");

        amountAct.Should().Throw<ArgumentOutOfRangeException>();
        currencyAct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payment_requires_an_order_id()
    {
        var act = () => Payment.Create(Guid.Empty, 1, "USD");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("US")]
    [InlineData(" ")]
    public void Payment_requires_a_valid_currency(string? currency)
    {
        var act = () => Payment.Create(Guid.NewGuid(), 1, currency!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payment_requires_a_provider_reference_for_an_attempt()
    {
        var payment = Payment.Create(Guid.NewGuid(), 1, "USD");

        var act = () => payment.RecordAuthorizationAttempt(true, " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payment_attempt_uses_supplied_id_and_time()
    {
        var payment = Payment.Create(Guid.NewGuid(), 1, "USD");
        var attemptId = Guid.NewGuid();
        var attemptedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var attempt = payment.RecordAuthorizationAttempt(true, " reference ", attemptedAt: attemptedAt, id: attemptId);

        attempt.Id.Should().Be(attemptId);
        attempt.AttemptedAt.Should().Be(attemptedAt);
        attempt.ProviderReference.Should().Be("reference");
    }
}
