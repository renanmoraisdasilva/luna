using Luna.Catalog.Application.Common;

namespace Luna.Catalog.Application.Errors;

internal static class CatalogErrors
{
    internal static readonly ApiError ProductNotFound = new(
        "PRODUCT_NOT_FOUND",
        "The product could not be found.");

    internal static readonly ApiError CategoryNotFound = new(
        "CATEGORY_NOT_FOUND",
        "The category could not be found.");
}
