using FluentAssertions;
using Luna.Payments.Domain;
using Luna.Payments.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.UnitTests.Payments;

public sealed class PaymentDbContextTests
{
    [Fact]
    public void Newly_added_payment_attempts_are_marked_for_insert()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PaymentsModelTest;Trusted_Connection=True;")
            .Options;

        using var db = new PaymentDbContext(options);
        var payment = Payment.Create(Guid.NewGuid(), 10m, "USD");

        db.Payments.Attach(payment);
        payment.RecordAuthorizationAttempt(succeeded: true, providerReference: "prov-1");
        db.ChangeTracker.DetectChanges();

        db.ChangeTracker.Entries<PaymentAttempt>()
            .Single()
            .State
            .Should()
            .Be(EntityState.Added);
    }
}
