using FluentAssertions;
using Luna.Catalog.Application.Products;
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

    [Fact]
    public async Task Returns_repository_result_and_forwards_product_id()
    {
        var product = new ProductResponse(
            Guid.NewGuid(), "LUNA-TEST-01", "Test Product", "A test product.", 19.95m,
            Guid.NewGuid(), "Electronics", "electronics", []);
        var repository = new FakeProductReadRepository(product);

        var result = await new GetProductHandler(repository).HandleAsync(product.Id, CancellationToken.None);

        result.Should().BeSameAs(product);
        repository.Id.Should().Be(product.Id);
    }

    private sealed class FakeProductReadRepository(ProductResponse? product = null) : IProductReadRepository
    {
        public Guid? Id { get; private set; }

        public Task<PaginatedResponse<ProductResponse>> GetActiveProductsAsync(
            string? search,
            string? categorySlug,
            PaginationRequest pagination,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PaginatedResponse<ProductResponse>([], pagination.Page, pagination.PageSize, 0));

        public Task<ProductResponse?> GetActiveProductAsync(Guid id, CancellationToken cancellationToken)
        {
            Id = id;
            return Task.FromResult(product);
        }
    }
}
