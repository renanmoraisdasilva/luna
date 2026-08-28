    using Luna.Catalog.Application.Products;
    using Luna.Contracts.Pagination;
    using Luna.Catalog.Contracts.Products;
    using Luna.Catalog.Domain;
    using Luna.Catalog.Infrastructure.Database;
    using Microsoft.EntityFrameworkCore;
    using System.Linq.Expressions;

    namespace Luna.Catalog.Infrastructure.Repositories;

    public sealed class ProductReadRepository(CatalogDbContext db) : IProductReadRepository
    {
        private static readonly Expression<Func<Product, ProductResponse>> ProductProjection = product => new(
            product.Id,
            product.Sku,
            product.Name,
            product.Description,
            product.CurrentPrice,
            product.CategoryId,
            product.Category!.Name,
            product.Category.Slug,
            product.Images
                .OrderBy(image => image.DisplayOrder)
                .Select(image => new ProductImageResponse(image.ImageUrl, image.AltText, image.DisplayOrder))
                .ToList());

        public async Task<PaginatedResponse<ProductResponse>> GetActiveProductsAsync(
            string? search,
            string? categorySlug,
            PaginationRequest pagination,
            CancellationToken cancellationToken)
        {
            var query = db.Products
                .AsNoTracking()
                .Where(product => product.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim();
                query = query.Where(product => product.Name.Contains(normalizedSearch));
            }

            if (!string.IsNullOrWhiteSpace(categorySlug))
            {
                var normalizedCategory = categorySlug.Trim();
                query = query.Where(product => product.Category != null && product.Category.Slug == normalizedCategory);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(ProductProjection)
                .ToListAsync(cancellationToken);

            return new PaginatedResponse<ProductResponse>(items, pagination.Page, pagination.PageSize, totalCount);
        }

        public Task<ProductResponse?> GetActiveProductAsync(Guid id, CancellationToken cancellationToken) => db.Products
            .AsNoTracking()
            .Where(product => product.Id == id && product.IsActive)
            .Select(ProductProjection)
            .SingleOrDefaultAsync(cancellationToken);
    }
