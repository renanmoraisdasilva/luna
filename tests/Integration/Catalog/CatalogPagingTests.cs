using FluentAssertions;
using Luna.Catalog.Domain;
using Luna.Contracts.Pagination;
using Xunit;

namespace Luna.IntegrationTests.Catalog;

/// <summary>
/// Tests that the catalog read surface refuses an unbounded page size.
/// </summary>
/// <remarks>
/// Catalog is the only anonymously reachable read endpoint in the system: <c>CatalogController</c> carries no
/// <c>[Authorize]</c> and Catalog registers bare <c>AddAuthorization()</c>. A page size with no ceiling
/// therefore let any anonymous caller ask the database to materialise the whole product table in one response,
/// including every product's description and image collection.
/// </remarks>
[Collection(CatalogDatabaseCollection.Name)]
public sealed class CatalogPagingTests : IAsyncLifetime
{
    private readonly CatalogSqlServerFixture fixture;

    public CatalogPagingTests(CatalogSqlServerFixture fixture) => this.fixture = fixture;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedProductsAsync(3);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_a_page_size_above_the_ceiling()
    {
        await using var db = fixture.CreateDbContext();

        var act = () => new Luna.Catalog.Infrastructure.Repositories.ProductReadRepository(db)
            .GetActiveProductsAsync(null, null, new PaginationRequest(Page: 1, PageSize: 2_000_000_000), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("PageSize");
    }

    [Fact]
    public async Task Rejects_a_page_size_of_zero()
    {
        await using var db = fixture.CreateDbContext();

        var act = () => new Luna.Catalog.Infrastructure.Repositories.ProductReadRepository(db)
            .GetActiveProductsAsync(null, null, new PaginationRequest(Page: 1, PageSize: 0), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("PageSize");
    }

    [Fact]
    public async Task Rejects_a_page_size_above_the_ceiling_for_categories()
    {
        await using var db = fixture.CreateDbContext();

        var act = () => new Luna.Catalog.Infrastructure.Repositories.CategoryReadRepository(db)
            .GetCategoriesAsync(new PaginationRequest(Page: 1, PageSize: 2_000_000_000), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("PageSize");
    }

    [Fact]
    public async Task Rejects_a_page_below_one()
    {
        await using var db = fixture.CreateDbContext();

        var act = () => new Luna.Catalog.Infrastructure.Repositories.ProductReadRepository(db)
            .GetActiveProductsAsync(null, null, new PaginationRequest(Page: 0, PageSize: 20), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("Page");
    }

    [Fact]
    public async Task Still_serves_a_page_size_at_the_ceiling()
    {
        // Guards against the ceiling being applied where it should not be. A legitimate caller asking for the
        // maximum allowed page must still succeed.
        await using var db = fixture.CreateDbContext();

        var response = await new Luna.Catalog.Infrastructure.Repositories.ProductReadRepository(db)
            .GetActiveProductsAsync(null, null, new PaginationRequest(Page: 1, PageSize: PaginationRequest.MaxPageSize), default);

        response.Items.Should().HaveCount(3);
        response.PageSize.Should().Be(PaginationRequest.MaxPageSize);
    }

    private async Task SeedProductsAsync(int count)
    {
        await using var db = fixture.CreateDbContext();

        for (var index = 0; index < count; index++)
        {
            db.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = $"Category {index}",
                Slug = $"category-{index}",
            });
        }

        await db.SaveChangesAsync();

        var category = db.Categories.First();
        for (var index = 0; index < count; index++)
        {
            db.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Sku = $"SKU-{index}",
                Name = $"Product {index}",
                Description = "A product used by the catalog paging tests.",
                CurrentPrice = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }
}
