using FluentAssertions;
using Luna.Shipping.Domain;
using Luna.Shipping.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Luna.UnitTests.Shipping;

public sealed class ShippingDbContextTests
{
    [Fact]
    public void Newly_added_tracking_events_are_marked_for_insert()
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ShippingModelTest;Trusted_Connection=True;")
            .Options;

        using var db = new ShippingDbContext(options);
        var shipment = Shipment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LUNA-123",
            ShipmentRecipientSnapshot.Create(
                Guid.NewGuid(),
                "Jane Doe",
                "1 Main Street",
                null,
                "Austin",
                "Texas",
                "78701",
                "US"));

        db.Shipments.Attach(shipment);
        shipment.MarkInTransit();
        db.ChangeTracker.DetectChanges();

        db.ChangeTracker.Entries<TrackingEvent>()
            .Single(entry => entry.Entity.Status == nameof(ShipmentStatus.InTransit))
            .State
            .Should()
            .Be(EntityState.Added);
    }
}
