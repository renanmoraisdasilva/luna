using FluentAssertions;
using Luna.Catalog.Domain;
using Luna.Catalog.Contracts.Products;
using Luna.Contracts.Pagination;
using Luna.Catalog.Infrastructure.Repositories;
using Xunit;

namespace Luna.IntegrationTests.Catalog;

[Collection(CatalogDatabaseCollection.Name)]
public sealed class ProductReadRepositoryTests(CatalogSqlServerFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The database is reset as a lifecycle hook rather than as the first statement of each test, so a
    /// test that throws during setup cannot leak rows into the next one.
    /// </summary>
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Returns_active_products_ordered_with_projected_images()
    {
        await using var db = fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics" };
        var firstProduct = CreateProduct(category, "Keyboard", isActive: true);
        var secondProduct = CreateProduct(category, "Mouse", isActive: true);
        firstProduct.AddImage("second.jpg", "Second", displayOrder: 1);
        firstProduct.AddImage("first.jpg", "First", displayOrder: 0);
        db.AddRange(category, firstProduct, secondProduct);
        await db.SaveChangesAsync();

        var result = await new ProductReadRepository(db).GetActiveProductsAsync(
            null, null, new PaginationRequest(Page: 1, PageSize: 2), CancellationToken.None);

        result.Items.Select(product => product.Name).Should().Equal("Keyboard", "Mouse");
        result.Items[0].CategoryName.Should().Be("Electronics");
        result.Items[0].CategorySlug.Should().Be("electronics");
        result.Items[0].Images.Select(image => image.ImageUrl).Should().Equal("first.jpg", "second.jpg");
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Applies_case_insensitive_search_and_category_filters()
    {
        await using var db = fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics" };
        db.AddRange(
            category,
            CreateProduct(category, "Mechanical Keyboard", isActive: true),
            CreateProduct(category, "Desk Lamp", isActive: true),
            CreateProduct(category, "Inactive Keyboard", isActive: false));
        await db.SaveChangesAsync();

        var result = await new ProductReadRepository(db).GetActiveProductsAsync(
            "  KEYBOARD  ", "ELECTRONICS", new PaginationRequest(), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Mechanical Keyboard");
    }

    [Fact]
    public async Task Returns_requested_page_with_total_count()
    {
        await using var db = fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics" };
        db.AddRange(
            category,
            CreateProduct(category, "Keyboard", isActive: true),
            CreateProduct(category, "Mouse", isActive: true),
            CreateProduct(category, "Monitor", isActive: true));
        await db.SaveChangesAsync();

        var result = await new ProductReadRepository(db).GetActiveProductsAsync(
            null, null, new PaginationRequest(Page: 2, PageSize: 2), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Mouse");
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Returns_only_active_product_for_detail_lookup()
    {
        await using var db = fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics" };
        var activeProduct = CreateProduct(category, "Active", isActive: true);
        var inactiveProduct = CreateProduct(category, "Inactive", isActive: false);
        db.AddRange(category, activeProduct, inactiveProduct);
        await db.SaveChangesAsync();

        var repository = new ProductReadRepository(db);

        var activeResult = await repository.GetActiveProductAsync(activeProduct.Id, CancellationToken.None);
        var inactiveResult = await repository.GetActiveProductAsync(inactiveProduct.Id, CancellationToken.None);
        var missingResult = await repository.GetActiveProductAsync(Guid.NewGuid(), CancellationToken.None);

        activeResult.Should().NotBeNull();
        activeResult!.Name.Should().Be("Active");
        inactiveResult.Should().BeNull();
        missingResult.Should().BeNull();
    }

    private static Product CreateProduct(Category category, string name, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        CategoryId = category.Id,
        Category = category,
        Sku = $"SKU-{Guid.NewGuid():N}",
        Name = name,
        Description = $"{name} description",
        CurrentPrice = 19.95m,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
