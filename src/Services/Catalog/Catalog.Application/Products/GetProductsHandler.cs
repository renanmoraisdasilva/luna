using Luna.Contracts.Pagination;
using Luna.Catalog.Contracts.Products;

namespace Luna.Catalog.Application.Products;

public sealed class GetProductsHandler(IProductReadRepository repository)
{
    public Task<PaginatedResponse<ProductResponse>> HandleAsync(
        string? search,
        string? categorySlug,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        return repository.GetActiveProductsAsync(search, categorySlug, pagination, cancellationToken);
    }
}
