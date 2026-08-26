namespace Luna.Catalog.Domain;

public sealed class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public required string ImageUrl { get; set; }
    public required string AltText { get; set; }
    public int DisplayOrder { get; set; }
    public Product? Product { get; set; }
}
