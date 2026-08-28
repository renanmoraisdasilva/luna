using Luna.Contracts.Pagination;
using Luna.Catalog.Contracts.Categories;

namespace Luna.Catalog.Application.Categories;

public interface ICategoryReadRepository
{
    Task<PaginatedResponse<CategoryResponse>> GetCategoriesAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken);
}
