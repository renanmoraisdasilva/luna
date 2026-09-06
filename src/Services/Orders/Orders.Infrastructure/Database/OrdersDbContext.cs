using Luna.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Orders.Infrastructure.Database;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

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
    }
}
