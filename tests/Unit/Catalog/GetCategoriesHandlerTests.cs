using FluentAssertions;
using Luna.Catalog.Application.Categories;
using Luna.Contracts.Pagination;
using Xunit;

namespace Luna.UnitTests.Catalog;

public sealed class GetCategoriesHandlerTests
{
    [Fact]
    public async Task Returns_repository_result()
    {
        var categories = new[]
        {
            new CategoryResponse(Guid.NewGuid(), "Home", "home"),
            new CategoryResponse(Guid.NewGuid(), "Electronics", "electronics")
        };

        var paginated = new PaginatedResponse<CategoryResponse>(categories, 1, 20, categories.Length);
        var result = await new GetCategoriesHandler(new FakeCategoryReadRepository(paginated))
            .HandleAsync(new PaginationRequest(), CancellationToken.None);

        result.Should().BeSameAs(paginated);
    }

    private sealed class FakeCategoryReadRepository(PaginatedResponse<CategoryResponse> result) : ICategoryReadRepository
    {
        public Task<PaginatedResponse<CategoryResponse>> GetCategoriesAsync(
            PaginationRequest pagination,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
