using Luna.Catalog.Contracts.Categories;
using Luna.Contracts.Pagination;

namespace Luna.Catalog.Application.Categories;

public sealed class GetCategoriesHandler(ICategoryReadRepository repository)
{
    public Task<PaginatedResponse<CategoryResponse>> HandleAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        return repository.GetCategoriesAsync(pagination, cancellationToken);
    }
}
