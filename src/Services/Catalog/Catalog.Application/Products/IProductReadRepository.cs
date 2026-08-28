using Luna.Contracts.Pagination;
using Luna.Catalog.Contracts.Products;

namespace Luna.Catalog.Application.Products;

public interface IProductReadRepository
{
    Task<PaginatedResponse<ProductResponse>> GetActiveProductsAsync(
        string? search,
        string? categorySlug,
        PaginationRequest pagination,
        CancellationToken cancellationToken);
    Task<ProductResponse?> GetActiveProductAsync(Guid id, CancellationToken cancellationToken);
}
