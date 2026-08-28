using Luna.Catalog.Application.Categories;
using Luna.Catalog.Application.Errors;
using Luna.Catalog.Application.Products;
using Luna.Contracts.Pagination;
using Luna.Catalog.Contracts.Categories;
using Luna.Catalog.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace Luna.Catalog.Controllers;

[ApiController]
[Route("api/v1/catalog")]
public sealed class CatalogController(
    GetProductsHandler getProducts,
    GetProductHandler getProduct,
    GetCategoriesHandler getCategories) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<PaginatedResponse<ProductResponse>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var products = await getProducts.HandleAsync(search, category, pagination, cancellationToken);
        return Ok(products);
    }

    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(Guid id, CancellationToken cancellationToken)
    {
        var product = await getProduct.HandleAsync(id, cancellationToken);
        return product is null
            ? NotFound(CatalogErrors.ProductNotFound)
            : Ok(product);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<PaginatedResponse<CategoryResponse>>> GetCategories(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        var categories = await getCategories.HandleAsync(pagination, cancellationToken);
        return Ok(categories);
    }
}
