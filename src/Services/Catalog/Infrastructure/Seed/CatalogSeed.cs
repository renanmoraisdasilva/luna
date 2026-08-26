using Luna.Catalog.Domain;
using Luna.Catalog.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Luna.Catalog.Infrastructure.Seed;

public static class CatalogSeed
{
    public static readonly Guid ElectronicsCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid HomeCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid KeyboardId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid HeadphonesId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid LampId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Categories.AnyAsync(cancellationToken)) return;

        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Categories.AddRange(
            new Category { Id = ElectronicsCategoryId, Name = "Electronics", Slug = "electronics" },
            new Category { Id = HomeCategoryId, Name = "Home", Slug = "home" });
        db.Products.AddRange(
            new Product { Id = KeyboardId, CategoryId = ElectronicsCategoryId, Sku = "LUNA-KB-01", Name = "Orbit Mechanical Keyboard", Description = "A compact mechanical keyboard with quiet switches and a crisp aluminum frame.", CurrentPrice = 89.00m, IsActive = true, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Product { Id = HeadphonesId, CategoryId = ElectronicsCategoryId, Sku = "LUNA-HL-01", Name = "Nightfall Headphones", Description = "Comfortable wireless headphones tuned for long listening sessions.", CurrentPrice = 129.00m, IsActive = true, CreatedAt = createdAt, UpdatedAt = createdAt },
            new Product { Id = LampId, CategoryId = HomeCategoryId, Sku = "LUNA-LP-01", Name = "Moonlit Desk Lamp", Description = "A warm adjustable lamp for late-night work and reading.", CurrentPrice = 54.00m, IsActive = true, CreatedAt = createdAt, UpdatedAt = createdAt });
        db.ProductImages.AddRange(
            new ProductImage { Id = Guid.Parse("aaaa0001-0001-0001-0001-000000000001"), ProductId = KeyboardId, ImageUrl = "https://images.unsplash.com/photo-1587829741301-dc798b83add3?auto=format&fit=crop&w=1200&q=85", AltText = "Orbit mechanical keyboard", DisplayOrder = 0 },
            new ProductImage { Id = Guid.Parse("bbbb0001-0001-0001-0001-000000000001"), ProductId = HeadphonesId, ImageUrl = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=1200&q=85", AltText = "Nightfall wireless headphones", DisplayOrder = 0 },
            new ProductImage { Id = Guid.Parse("cccc0001-0001-0001-0001-000000000001"), ProductId = LampId, ImageUrl = "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=1200&q=85", AltText = "Moonlit desk lamp", DisplayOrder = 0 });
        await db.SaveChangesAsync(cancellationToken);
    }
}
