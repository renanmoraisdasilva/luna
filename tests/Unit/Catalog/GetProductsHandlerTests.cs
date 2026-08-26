using FluentAssertions;
using Luna.Catalog.Application.Products;
using Luna.Contracts.Pagination;
using Xunit;

namespace Luna.UnitTests.Catalog;

public sealed class GetProductsHandlerTests
{
    [Fact]
    public async Task Returns_repository_result_and_forwards_filters()
    {
        var product = new ProductResponse(
            Guid.NewGuid(), "LUNA-TEST-01", "Test Product", "A test product.", 19.95m,
            Guid.NewGuid(), "Electronics", "electronics",
            [new ProductImageResponse("first.jpg", "First", 0), new ProductImageResponse("second.jpg", "Second", 1)]);
        var paginated = new PaginatedResponse<ProductResponse>([product], 1, 20, 1);
        var repository = new FakeProductReadRepository(paginated);

        var result = await new GetProductsHandler(repository).HandleAsync(
            "test", "electronics", new PaginationRequest(), CancellationToken.None);

        result.Should().BeSameAs(paginated);
        repository.Search.Should().Be("test");
        repository.CategorySlug.Should().Be("electronics");
    }

    [Fact]
    public async Task Forwards_pagination_without_normalizing()
    {
        var pagination = new PaginationRequest(Page: 2, PageSize: 500);
        var repository = new FakeProductReadRepository(
            new PaginatedResponse<ProductResponse>([], pagination.Page, pagination.PageSize, 0));

        await new GetProductsHandler(repository).HandleAsync(
            null, null, pagination, CancellationToken.None);

        repository.Pagination.Should().BeSameAs(pagination);
    }

    private sealed class FakeProductReadRepository : IProductReadRepository
    {
        private readonly PaginatedResponse<ProductResponse> result;

        public FakeProductReadRepository(PaginatedResponse<ProductResponse> result)
        {
            this.result = result;
        }

        public string? Search { get; private set; }
        public string? CategorySlug { get; private set; }
        public PaginationRequest? Pagination { get; private set; }
        public Task<PaginatedResponse<ProductResponse>> GetActiveProductsAsync(
            string? search,
            string? categorySlug,
            PaginationRequest pagination,
            CancellationToken cancellationToken)
        {
            Search = search;
            CategorySlug = categorySlug;
            Pagination = pagination;
            return Task.FromResult(result);
        }

        public Task<ProductResponse?> GetActiveProductAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ProductResponse?>(null);
    }
}
