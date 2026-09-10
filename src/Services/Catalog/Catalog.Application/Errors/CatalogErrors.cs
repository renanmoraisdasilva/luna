using Luna.Contracts.Errors;

namespace Luna.Catalog.Application.Errors;

public static class CatalogErrors
{
    public static readonly ApiError ProductNotFound = new(
        "PRODUCT_NOT_FOUND",
        "The product could not be found.");

    public static readonly ApiError CategoryNotFound = new(
        "CATEGORY_NOT_FOUND",
        "The category could not be found.");
}
