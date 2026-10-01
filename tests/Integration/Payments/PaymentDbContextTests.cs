using FluentAssertions;
using Luna.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Payments;

[Collection(PaymentsDatabaseCollection.Name)]
public sealed class PaymentDbContextTests(PaymentsSqlServerFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The database is reset as a lifecycle hook rather than as the first statement of each test, so a
    /// test that throws during setup cannot leak rows into the next one.
    /// </summary>
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_attempt_added_to_an_existing_payment_is_inserted()
    {
        var payment = await SeedPendingPaymentAsync();

        await using var db = fixture.CreateDbContext();
        var loaded = await db.Payments.SingleAsync(value => value.Id == payment.Id);
        loaded.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-1");
        db.ChangeTracker.DetectChanges();

        db.ChangeTracker.Entries<PaymentAttempt>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Added);

        // An attempt EF mistook for an existing row would be reported as an update, affect no rows, and
        // throw. Only a real database round trip proves the insert, which the original version of this test
        // — inspecting the change tracker against an unattached graph — could not.
        await db.SaveChangesAsync();

        await using var verificationDb = fixture.CreateDbContext();
        var persisted = await verificationDb.Payments
            .Include(value => value.Attempts)
            .SingleAsync(value => value.Id == payment.Id);
        persisted.Status.Should().Be(PaymentStatus.Authorized);
        persisted.Attempts.Select(value => value.ProviderReference).Should().ContainSingle("prov-1");
    }

    private async Task<Payment> SeedPendingPaymentAsync()
    {
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD");

        await using var db = fixture.CreateDbContext();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }
}
