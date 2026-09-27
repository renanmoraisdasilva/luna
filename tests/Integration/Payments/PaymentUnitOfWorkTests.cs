using FluentAssertions;
using Luna.Payments.Domain;
using Luna.Payments.Infrastructure;
using Luna.Payments.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Payments;

[Collection(PaymentsDatabaseCollection.Name)]
public sealed class PaymentUnitOfWorkTests(PaymentsSqlServerFixture fixture)
{
    [Fact]
    public async Task Saves_a_new_payment_and_its_authorization_attempt()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateDbContext();
        var payment = Payment.Create(Guid.NewGuid(), 25.50m, "USD");
        payment.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-1");
        db.Payments.Add(payment);
        var unitOfWork = new PaymentUnitOfWork(db);

        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var verificationDb = fixture.CreateDbContext();
        var persisted = await verificationDb.Payments
            .Include(value => value.Attempts)
            .SingleAsync(value => value.Id == payment.Id);
        persisted.Status.Should().Be(PaymentStatus.Authorized);
        persisted.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task Rejects_a_second_payment_for_the_same_order()
    {
        await fixture.ResetAsync();
        var orderId = Guid.NewGuid();
        await using var seedDb = fixture.CreateDbContext();
        seedDb.Payments.Add(Payment.Create(orderId, 10m, "USD"));
        await seedDb.SaveChangesAsync();

        await using var db = fixture.CreateDbContext();
        db.Payments.Add(Payment.Create(orderId, 12m, "USD"));
        var unitOfWork = new PaymentUnitOfWork(db);

        var act = () => unitOfWork.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A payment already exists for this order.");
    }

    [Fact]
    public async Task Propagates_a_primary_key_conflict_that_is_not_order_uniqueness()
    {
        await fixture.ResetAsync();
        var paymentId = Guid.NewGuid();
        await using var seedDb = fixture.CreateDbContext();
        seedDb.Payments.Add(Payment.Create(Guid.NewGuid(), 10m, "USD", paymentId));
        await seedDb.SaveChangesAsync();

        await using var db = fixture.CreateDbContext();
        db.Payments.Add(Payment.Create(Guid.NewGuid(), 12m, "USD", paymentId));
        var unitOfWork = new PaymentUnitOfWork(db);

        var exception = await Record.ExceptionAsync(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

        exception.Should().BeOfType<DbUpdateException>();
        exception!.InnerException.Should().BeOfType<SqlException>()
            .Which.Message.Should().Contain("PK_Payments");
    }

    [Fact]
    public async Task Propagates_a_row_version_conflict_from_a_concurrent_update()
    {
        await fixture.ResetAsync();
        var orderId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            var payment = Payment.Create(orderId, 10m, "USD");
            seedDb.Payments.Add(payment);
            await seedDb.SaveChangesAsync();
        }

        await using (var firstDb = fixture.CreateDbContext())
        {
            var payment = await firstDb.Payments.SingleAsync(value => value.OrderId == orderId);
            payment.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-first");
            await firstDb.SaveChangesAsync();
        }

        await using var staleDb = fixture.CreateDbContext();
        var stalePayment = await staleDb.Payments.SingleAsync(value => value.OrderId == orderId);
        stalePayment.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-second");
        var unitOfWork = new PaymentUnitOfWork(staleDb);

        var exception = await Record.ExceptionAsync(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

        exception.Should().BeOfType<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Propagates_a_constraint_violation_that_is_not_a_duplicate_key()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateDbContext();
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD");
        payment.RecordAuthorizationAttempt(succeeded: true, providerReference: new string('p', 400));
        db.Payments.Add(payment);
        var unitOfWork = new PaymentUnitOfWork(db);

        var exception = await Record.ExceptionAsync(() => unitOfWork.SaveChangesAsync(CancellationToken.None));

        exception.Should().BeOfType<DbUpdateException>();
        var sqlException = exception!.InnerException.Should().BeOfType<SqlException>().Which;
        sqlException.Number.Should().NotBe(2601);
        sqlException.Number.Should().NotBe(2627);
    }
}
