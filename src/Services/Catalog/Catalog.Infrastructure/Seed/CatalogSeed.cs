using Luna.Catalog.Domain;
using Luna.Catalog.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Luna.Catalog.Infrastructure.Seed;

public static class CatalogSeed
{
    public static readonly Guid ElectronicsCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid HomeCategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OfficeCategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid OutdoorCategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid KeyboardId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid HeadphonesId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid LampId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Categories.AnyAsync(cancellationToken)) return;

        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Categories.AddRange(
            new Category { Id = ElectronicsCategoryId, Name = "Electronics", Slug = "electronics" },
            new Category { Id = HomeCategoryId, Name = "Home", Slug = "home" },
            new Category { Id = OfficeCategoryId, Name = "Office", Slug = "office" },
            new Category { Id = OutdoorCategoryId, Name = "Outdoor", Slug = "outdoor" });

        var products = new[]
        {
            new Product { Id = KeyboardId, CategoryId = ElectronicsCategoryId, Sku = "LUNA-KB-01", Name = "Orbit Mechanical Keyboard", Description = "A compact mechanical keyboard with quiet switches and a crisp aluminum frame.", CurrentPrice = 89.00m },
            new Product { Id = HeadphonesId, CategoryId = ElectronicsCategoryId, Sku = "LUNA-HL-01", Name = "Nightfall Headphones", Description = "Comfortable wireless headphones tuned for long listening sessions.", CurrentPrice = 129.00m },
            new Product { Id = LampId, CategoryId = HomeCategoryId, Sku = "LUNA-LP-01", Name = "Moonlit Desk Lamp", Description = "A warm adjustable lamp for late-night work and reading.", CurrentPrice = 54.00m },
            new Product { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), CategoryId = ElectronicsCategoryId, Sku = "LUNA-MO-01", Name = "Comet Wireless Mouse", Description = "An ergonomic wireless mouse with precise tracking and silent clicks.", CurrentPrice = 39.00m },
            new Product { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), CategoryId = ElectronicsCategoryId, Sku = "LUNA-HU-01", Name = "Nova USB Hub", Description = "A compact USB-C hub with fast data ports and HDMI output.", CurrentPrice = 49.00m },
            new Product { Id = Guid.Parse("10101010-1010-1010-1010-101010101010"), CategoryId = ElectronicsCategoryId, Sku = "LUNA-SP-01", Name = "Echo Bluetooth Speaker", Description = "A portable speaker with balanced sound and all-day battery life.", CurrentPrice = 79.00m },
            new Product { Id = Guid.Parse("12121212-1212-1212-1212-121212121212"), CategoryId = HomeCategoryId, Sku = "LUNA-OR-01", Name = "Orbit Wall Clock", Description = "A clean modern wall clock with a quiet sweeping movement.", CurrentPrice = 42.00m },
            new Product { Id = Guid.Parse("14141414-1414-1414-1414-141414141414"), CategoryId = HomeCategoryId, Sku = "LUNA-MG-01", Name = "Sage Ceramic Mug", Description = "A durable ceramic mug with a comfortable rounded handle.", CurrentPrice = 18.00m },
            new Product { Id = Guid.Parse("15151515-1515-1515-1515-151515151515"), CategoryId = HomeCategoryId, Sku = "LUNA-BL-01", Name = "Cloud Knit Blanket", Description = "A soft textured blanket for comfortable evenings at home.", CurrentPrice = 64.00m },
            new Product { Id = Guid.Parse("16161616-1616-1616-1616-161616161616"), CategoryId = HomeCategoryId, Sku = "LUNA-PL-01", Name = "Terra Planter", Description = "A minimalist planter that brings a natural accent to any room.", CurrentPrice = 28.00m },
            new Product { Id = Guid.Parse("17171717-1717-1717-1717-171717171717"), CategoryId = OfficeCategoryId, Sku = "LUNA-NB-01", Name = "Field Notes Notebook", Description = "A premium dot-grid notebook for ideas, plans, and daily notes.", CurrentPrice = 16.00m },
            new Product { Id = Guid.Parse("18181818-1818-1818-1818-181818181818"), CategoryId = OfficeCategoryId, Sku = "LUNA-PH-01", Name = "Summit Monitor Stand", Description = "A sturdy monitor stand that raises your screen and adds desk storage.", CurrentPrice = 74.00m },
            new Product { Id = Guid.Parse("19191919-1919-1919-1919-191919191919"), CategoryId = OfficeCategoryId, Sku = "LUNA-DS-01", Name = "Focus Desk Pad", Description = "A smooth desk pad that protects your surface and improves mouse control.", CurrentPrice = 26.00m },
            new Product { Id = Guid.Parse("20202020-2020-2020-2020-202020202020"), CategoryId = OfficeCategoryId, Sku = "LUNA-PE-01", Name = "Graphite Pen Set", Description = "A set of smooth-writing gel pens for everyday desk work.", CurrentPrice = 14.00m },
            new Product { Id = Guid.Parse("21212121-2121-2121-2121-212121212121"), CategoryId = OfficeCategoryId, Sku = "LUNA-OR-02", Name = "Atlas Organizer", Description = "A modular desktop organizer for cables, stationery, and small tools.", CurrentPrice = 36.00m },
            new Product { Id = Guid.Parse("23232323-2323-2323-2323-232323232323"), CategoryId = OutdoorCategoryId, Sku = "LUNA-BP-01", Name = "Trail Daypack", Description = "A lightweight daypack with practical compartments for short adventures.", CurrentPrice = 92.00m },
            new Product { Id = Guid.Parse("24242424-2424-2424-2424-242424242424"), CategoryId = OutdoorCategoryId, Sku = "LUNA-BT-01", Name = "Summit Water Bottle", Description = "A vacuum-insulated bottle that keeps drinks cold throughout the day.", CurrentPrice = 31.00m },
        }.Select(product =>
        {
            product.IsActive = true;
            product.CreatedAt = createdAt;
            product.UpdatedAt = createdAt;
            return product;
        }).ToArray();
        db.Products.AddRange(products);

        var productImages = new Dictionary<string, string[]>
        {
            ["LUNA-KB-01"] = new[] { "https://images.unsplash.com/photo-1587829741301-dc798b83add3?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1511467687858-23d96c32e4ae?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-HL-01"] = new[] { "https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-LP-01"] = new[] { "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1494438639946-1ebd1d20bf85?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-MO-01"] = new[] { "https://images.unsplash.com/photo-1527814050087-3793815479db?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1547119957-637f8679db1e?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-HU-01"] = new[] { "https://images.unsplash.com/photo-1504274066651-8d31a536b11a?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-SP-01"] = new[] { "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-OR-01"] = new[] { "https://images.unsplash.com/photo-1563861826100-9cb868fdbe1c?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1595428774223-ef52624120d2?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-MG-01"] = new[] { "https://images.unsplash.com/photo-1514228742587-6b1558fcca3d?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-BL-01"] = new[] { "https://images.unsplash.com/photo-1604176354204-9268737828e4?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1616627561950-9f746e330187?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-PL-01"] = new[] { "https://images.unsplash.com/photo-1485955900006-10f4d324d411?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-NB-01"] = new[] { "https://images.unsplash.com/photo-1456324504439-367cee3b3c32?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-PH-01"] = new[] { "https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?auto=format&fit=crop&w=1200&q=85", "https://images.unsplash.com/photo-1499951360447-b19be8fe80f5?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-DS-01"] = new[] { "https://images.unsplash.com/photo-1499750310107-5fef28a66643?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-PE-01"] = new[] { "https://images.unsplash.com/photo-1455390582262-044cdead277a?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-OR-02"] = new[] { "https://images.unsplash.com/photo-1473445361085-b9a07f55608b?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-BP-01"] = new[] { "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=1200&q=85" },
            ["LUNA-BT-01"] = new[] { "https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=1200&q=85" },
        };

        var imageId = 1;
        foreach (var product in products)
        {
            var displayOrder = 0;
            foreach (var url in productImages[product.Sku])
            {
                product.AddImage(
                    url,
                    displayOrder == 0 ? product.Name : $"{product.Name} alternate view",
                    displayOrder: displayOrder,
                    id: Guid.Parse($"{imageId++:00000000}-0000-0000-0000-000000000001"));
                displayOrder++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
