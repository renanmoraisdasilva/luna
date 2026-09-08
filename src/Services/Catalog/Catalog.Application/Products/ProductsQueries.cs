using Luna.Catalog.Contracts.Products;
using Luna.Contracts.Pagination;

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

public sealed class GetProductHandler(IProductReadRepository repository)
{
    public Task<ProductResponse?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        return repository.GetActiveProductAsync(id, cancellationToken);
    }
}
