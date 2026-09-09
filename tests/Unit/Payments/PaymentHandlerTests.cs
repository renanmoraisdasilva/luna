using FluentAssertions;
using Luna.Payments.Application.Authorization;
using Luna.Payments.Domain;
using Xunit;

namespace Luna.UnitTests.Payments;

public sealed class PaymentHandlerTests
{
    [Fact]
    public async Task Handler_returns_authorized_response_and_persists_attempt()
    {
        var repository = new FakePaymentRepository();
        var unitOfWork = new FakePaymentUnitOfWork();
        var provider = new FakeProvider(true);
        var handler = new AuthorizePaymentHandler(repository, unitOfWork, provider);

        var response = await handler.HandleAsync(
            new AuthorizePaymentCommand(Guid.NewGuid(), 25.00m, "USD", "approved"),
            CancellationToken.None);

        response.Authorized.Should().BeTrue();
        response.Status.Should().Be(nameof(PaymentStatus.Authorized));
        repository.Payment!.Attempts.Should().ContainSingle();
        unitOfWork.SaveCalled.Should().BeTrue();
        provider.IdempotencyKey.Should().Be($"payment-authorization:{repository.Payment.OrderId:N}");
    }

    [Fact]
    public async Task Handler_returns_failed_response_for_deterministic_provider_rejection()
    {
        var repository = new FakePaymentRepository();
        var handler = new AuthorizePaymentHandler(repository, new FakePaymentUnitOfWork(), new FakeProvider(false));

        var response = await handler.HandleAsync(
            new AuthorizePaymentCommand(Guid.NewGuid(), 25.00m, "USD", "declined"),
            CancellationToken.None);

        response.Authorized.Should().BeFalse();
        response.Status.Should().Be(nameof(PaymentStatus.Failed));
        response.FailureCode.Should().Be("PAYMENT_DECLINED");
        repository.Payment!.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task Handler_rejects_empty_order_id()
    {
        var handler = new AuthorizePaymentHandler(new FakePaymentRepository(), new FakePaymentUnitOfWork(), new FakeProvider(true));

        var act = () => handler.HandleAsync(
            new AuthorizePaymentCommand(Guid.Empty, 25.00m, "USD", "approved"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handler_rejects_non_positive_amount()
    {
        var handler = new AuthorizePaymentHandler(new FakePaymentRepository(), new FakePaymentUnitOfWork(), new FakeProvider(true));

        var act = () => handler.HandleAsync(
            new AuthorizePaymentCommand(Guid.NewGuid(), 0, "USD", "approved"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Handler_rejects_missing_payment_method()
    {
        var handler = new AuthorizePaymentHandler(new FakePaymentRepository(), new FakePaymentUnitOfWork(), new FakeProvider(true));

        var act = () => handler.HandleAsync(
            new AuthorizePaymentCommand(Guid.NewGuid(), 25.00m, "USD", " "),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handler_authorizes_an_existing_payment_with_matching_amount_and_currency()
    {
        var payment = Payment.Create(Guid.NewGuid(), 25.00m, "USD");
        var repository = new FakePaymentRepository(payment);
        var handler = new AuthorizePaymentHandler(repository, new FakePaymentUnitOfWork(), new FakeProvider(true));

        var response = await handler.HandleAsync(
            new AuthorizePaymentCommand(payment.OrderId, 25.00m, " usd ", "approved"),
            CancellationToken.None);

        response.Authorized.Should().BeTrue();
        repository.Payment.Should().BeSameAs(payment);
    }

    [Theory]
    [InlineData(30.00, "USD")]
    [InlineData(25.00, "EUR")]
    public async Task Handler_rejects_an_existing_payment_with_mismatched_terms(decimal amount, string currency)
    {
        var payment = Payment.Create(Guid.NewGuid(), 25.00m, "USD");
        var handler = new AuthorizePaymentHandler(
            new FakePaymentRepository(payment),
            new FakePaymentUnitOfWork(),
            new FakeProvider(true));

        var act = () => handler.HandleAsync(
            new AuthorizePaymentCommand(payment.OrderId, amount, currency, "approved"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FakeProvider(bool succeeds) : IPaymentProvider
    {
        public string? IdempotencyKey { get; private set; }

        public Task<PaymentProviderAuthorization> AuthorizeAsync(
            Guid orderId,
            decimal amount,
            string currency,
            string paymentMethod,
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            RecordCall(idempotencyKey, succeeds
                ? new PaymentProviderAuthorization(true, "fake-reference", null)
                : new PaymentProviderAuthorization(false, "fake-reference", "PAYMENT_DECLINED"));

        private Task<PaymentProviderAuthorization> RecordCall(
            string idempotencyKey,
            PaymentProviderAuthorization authorization)
        {
            IdempotencyKey = idempotencyKey;
            return Task.FromResult(authorization);
        }
    }

    private sealed class FakePaymentRepository : IPaymentWriteRepository
    {
        public FakePaymentRepository(Payment? payment = null)
        {
            Payment = payment;
        }

        public Payment? Payment { get; private set; }

        public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Payment);

        public Task AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            Payment = payment;
            return Task.CompletedTask;
        }

    }

    private sealed class FakePaymentUnitOfWork : IPaymentUnitOfWork
    {
        public bool SaveCalled { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalled = true;
            return Task.CompletedTask;
        }
    }
}
