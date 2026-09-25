using Luna.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Orders.Infrastructure.Database;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(cart => cart.Id);
            entity.Property(cart => cart.Id).ValueGeneratedNever();
            entity.Property(cart => cart.CustomerId).IsRequired();
            entity.HasIndex(cart => cart.CustomerId).IsUnique();
            entity.Navigation(cart => cart.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(cart => cart.Items)
                .WithOne()
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.ProductId).IsRequired();
            entity.Property(item => item.Quantity).IsRequired();
            entity.HasIndex(item => new { item.CartId, item.ProductId }).IsUnique();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(order => order.Id);
            entity.Property(order => order.Id).ValueGeneratedNever();
            entity.Property(order => order.CustomerId).IsRequired();
            entity.Property(order => order.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(order => order.InventoryReservationId);
            entity.Property(order => order.PaymentId);
            entity.Property(order => order.ShippingQuoteId);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(order => order.ShippingMethodCode).HasMaxLength(40).IsRequired();
            entity.Property(order => order.ShippingCost).HasPrecision(18, 2).IsRequired();
            entity.Property(order => order.CreatedAt).IsRequired();
            entity.HasIndex(order => new { order.CustomerId, order.CreatedAt });
            entity.HasIndex(order => new { order.CustomerId, order.IdempotencyKey }).IsUnique();
            entity.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.OwnsOne(order => order.ShippingAddress, address =>
            {
                address.Property(value => value.FullName).HasMaxLength(160).IsRequired();
                address.Property(value => value.AddressLine1).HasMaxLength(200).IsRequired();
                address.Property(value => value.AddressLine2).HasMaxLength(200);
                address.Property(value => value.City).HasMaxLength(100).IsRequired();
                address.Property(value => value.StateOrProvince).HasMaxLength(100).IsRequired();
                address.Property(value => value.PostalCode).HasMaxLength(30).IsRequired();
                address.Property(value => value.Country).HasMaxLength(100).IsRequired();
            });
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.ProductId).IsRequired();
            entity.Property(item => item.Sku).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ProductName).HasMaxLength(240).IsRequired();
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(item => item.Quantity).IsRequired();
            entity.Property(item => item.LineTotal).HasPrecision(18, 2).IsRequired();
        });
    }
}
