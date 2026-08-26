namespace Luna.Catalog.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal CurrentPrice,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<ProductImageResponse> Images);

public sealed record ProductImageResponse(string ImageUrl, string AltText, int DisplayOrder);
