using Luna.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Luna.Catalog.Infrastructure.Database;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(120).IsRequired();
            entity.Property(category => category.Slug).HasMaxLength(120).IsRequired();
            entity.HasIndex(category => category.Name).IsUnique();
            entity.HasIndex(category => category.Slug).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Sku).HasMaxLength(80).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(2000).IsRequired();
            entity.Property(product => product.CurrentPrice).HasPrecision(18, 2).IsRequired();
            entity.HasIndex(product => product.Sku).IsUnique();
            entity.HasIndex(product => new { product.IsActive, product.Name });
            entity.HasOne(product => product.Category)
                .WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(image => image.Id);
            entity.Property(image => image.ImageUrl).HasMaxLength(1000).IsRequired();
            entity.Property(image => image.AltText).HasMaxLength(300).IsRequired();
            entity.HasIndex(image => new { image.ProductId, image.DisplayOrder });
            entity.HasOne(image => image.Product)
                .WithMany(product => product.Images)
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
