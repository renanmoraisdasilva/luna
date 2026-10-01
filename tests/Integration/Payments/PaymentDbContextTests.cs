using FluentAssertions;
using Luna.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.IntegrationTests.Payments;

[Collection(PaymentsDatabaseCollection.Name)]
public sealed class PaymentDbContextTests(PaymentsSqlServerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Newly_added_payment_attempts_are_marked_for_insert()
    {
        await using var db = fixture.CreateDbContext();
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD");

        db.Payments.Attach(payment);
        payment.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-1");
        db.ChangeTracker.DetectChanges();

        db.ChangeTracker.Entries<PaymentAttempt>()
            .Single()
            .State
            .Should()
            .Be(EntityState.Added);

        // The insert must survive the real database round trip: an attempt that
        // EF mistook for an existing row would be reported as an update.
        await db.SaveChangesAsync();

        await using var verificationDb = fixture.CreateDbContext();
        var persisted = await verificationDb.Payments
            .Include(value => value.Attempts)
            .SingleAsync(value => value.Id == payment.Id);
        persisted.Attempts.Should().ContainSingle();
    }
}
