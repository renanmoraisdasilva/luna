using FluentAssertions;
using Luna.Catalog.Application.Products;
using Luna.Catalog.Contracts.Products;
using Luna.Contracts.Pagination;
using Xunit;

namespace Luna.UnitTests.Catalog;

public sealed class GetProductHandlerTests
{
    [Fact]
    public async Task Returns_null_when_product_is_not_available()
    {
        var repository = new FakeProductReadRepository();

        var result = await new GetProductHandler(repository).HandleAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    private sealed class FakeProductReadRepository(ProductResponse? product = null) : IProductReadRepository
    {
        public Task<PaginatedResponse<ProductResponse>> GetActiveProductsAsync(
            string? search,
            string? categorySlug,
            PaginationRequest pagination,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PaginatedResponse<ProductResponse>([], pagination.Page, pagination.PageSize, 0));

        public Task<ProductResponse?> GetActiveProductAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(product);
    }
}
