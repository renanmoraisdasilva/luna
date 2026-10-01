using FluentAssertions;
using Luna.Catalog.Domain;
using Luna.Catalog.Contracts.Categories;
using Luna.Contracts.Pagination;
using Luna.Catalog.Infrastructure.Repositories;
using Xunit;

namespace Luna.IntegrationTests.Catalog;

[Collection(CatalogDatabaseCollection.Name)]
public sealed class CategoryReadRepositoryTests(CatalogSqlServerFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The database is reset as a lifecycle hook rather than as the first statement of each test, so a
    /// test that throws during setup cannot leak rows into the next one.
    /// </summary>
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Returns_categories_ordered_by_name_as_projections()
    {
        await using var db = fixture.CreateDbContext();
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
