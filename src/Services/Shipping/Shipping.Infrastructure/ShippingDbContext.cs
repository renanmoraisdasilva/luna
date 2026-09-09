using Luna.Shipping.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Shipping.Infrastructure;

public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<ShippingQuote> ShippingQuotes => Set<ShippingQuote>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<ShippingMethod>(entity =>
        {
            entity.HasKey(method => method.Id);
            entity.Property(method => method.Code).HasMaxLength(40).IsRequired();
            entity.Property(method => method.Name).HasMaxLength(120).IsRequired();
            entity.Property(method => method.Cost).HasPrecision(18, 2).IsRequired();
            entity.Property(method => method.EstimatedDeliveryDays).IsRequired();
            entity.HasIndex(method => method.Code).IsUnique();
        });

        modelBuilder.Entity<ShippingQuote>(entity =>
        {
            entity.HasKey(quote => quote.Id);
            entity.Property(quote => quote.ShippingMethodCode).HasMaxLength(40).IsRequired();
            entity.Property(quote => quote.Country).HasMaxLength(2).IsRequired();
            entity.Property(quote => quote.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(quote => quote.Cost).HasPrecision(18, 2).IsRequired();
            entity.Property(quote => quote.CreatedAt).IsRequired();
            entity.HasIndex(quote => quote.OrderId);
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(shipment => shipment.Id);
            entity.Property(shipment => shipment.TrackingNumber).HasMaxLength(40).IsRequired();
            entity.Property(shipment => shipment.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.HasIndex(shipment => shipment.OrderId).IsUnique();
            entity.HasIndex(shipment => shipment.TrackingNumber).IsUnique();
            entity.Navigation(shipment => shipment.TrackingEvents).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(shipment => shipment.TrackingEvents)
                .WithOne()
                .HasForeignKey(trackingEvent => trackingEvent.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrackingEvent>(entity =>
        {
            entity.HasKey(trackingEvent => trackingEvent.Id);
            entity.Property(trackingEvent => trackingEvent.Status).HasMaxLength(30).IsRequired();
            entity.Property(trackingEvent => trackingEvent.OccurredAt).IsRequired();
            entity.HasIndex(trackingEvent => new { trackingEvent.ShipmentId, trackingEvent.OccurredAt });
        });
    }
}
