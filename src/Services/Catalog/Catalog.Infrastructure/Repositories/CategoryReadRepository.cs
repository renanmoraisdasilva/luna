using Luna.Catalog.Application.Categories;
using Luna.Catalog.Domain;
using Luna.Contracts.Pagination;
using Luna.Catalog.Contracts.Categories;
using Luna.Catalog.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Luna.Catalog.Infrastructure.Repositories;

public sealed class CategoryReadRepository(CatalogDbContext db) : ICategoryReadRepository
{
    private static readonly Expression<Func<Category, CategoryResponse>> CategoryProjection =
        category => new(category.Id, category.Name, category.Slug);

    public async Task<PaginatedResponse<CategoryResponse>> GetCategoriesAsync(
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        pagination.Validate();

        var query = db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(CategoryProjection)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<CategoryResponse>(items, pagination.Page, pagination.PageSize, totalCount);
    }
}
