using Luna.Contracts.Pagination;

namespace Luna.Catalog.Application.Categories;

public interface ICategoryReadRepository
{
    Task<PaginatedResponse<CategoryResponse>> GetCategoriesAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken);
}
