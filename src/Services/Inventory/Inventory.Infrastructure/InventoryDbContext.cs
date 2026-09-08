using Luna.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Inventory.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<InventoryReservationItem> InventoryReservationItems => Set<InventoryReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(stock => stock.Id);
            entity.Property(stock => stock.ProductId).IsRequired();
            entity.Property(stock => stock.QuantityOnHand).IsRequired();
            entity.Property(stock => stock.QuantityReserved).IsRequired();
            entity.Property(stock => stock.RowVersion).IsRowVersion();
            entity.HasIndex(stock => stock.ProductId).IsUnique();
        });

        modelBuilder.Entity<InventoryReservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);
            entity.Property(reservation => reservation.OrderId).IsRequired();
            entity.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(reservation => reservation.RowVersion).IsRowVersion();
            entity.HasIndex(reservation => reservation.OrderId).IsUnique();
            entity.Navigation(reservation => reservation.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(reservation => reservation.Items)
                .WithOne()
                .HasForeignKey(item => item.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryReservationItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ProductId).IsRequired();
            entity.Property(item => item.Quantity).IsRequired();
            entity.HasIndex(item => new { item.ReservationId, item.ProductId }).IsUnique();
        });
    }
}
