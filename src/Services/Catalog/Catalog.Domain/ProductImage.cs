namespace Luna.Catalog.Domain;

public sealed class ProductImage
{
    private ProductImage() { }

    internal ProductImage(Guid id, Guid productId, string imageUrl, string altText, int displayOrder, Product product)
    {
        Id = id;
        ProductId = productId;
        ImageUrl = imageUrl;
        AltText = altText;
        DisplayOrder = displayOrder;
        Product = product;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string ImageUrl { get; private set; } = null!;
    public string AltText { get; private set; } = null!;
    public int DisplayOrder { get; private set; }
    public Product? Product { get; private set; }
}
