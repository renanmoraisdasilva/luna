using FluentAssertions;
using Luna.Catalog.Domain;
using Luna.Catalog.Contracts.Categories;
using Luna.Contracts.Pagination;
using Luna.Catalog.Infrastructure.Repositories;
using Xunit;

namespace Luna.IntegrationTests.Catalog;

[Collection(CatalogDatabaseCollection.Name)]
public sealed class CategoryReadRepositoryTests(CatalogSqlServerFixture fixture)
{
    [Fact]
    public async Task Returns_categories_ordered_by_name_as_projections()
    {
        await using var db = await fixture.CreateDbContextAsync();
        db.AddRange(
            new Category { Id = Guid.NewGuid(), Name = "Home", Slug = "home" },
            new Category { Id = Guid.NewGuid(), Name = "Electronics", Slug = "electronics" });
        await db.SaveChangesAsync();

        var result = await new CategoryReadRepository(db).GetCategoriesAsync(
            new PaginationRequest(Page: 1, PageSize: 2), CancellationToken.None);

        result.Items.Select(category => category.Name).Should().Equal("Electronics", "Home");
        result.Items.Select(category => category.Slug).Should().Equal("electronics", "home");
        result.TotalCount.Should().Be(2);
    }
}
