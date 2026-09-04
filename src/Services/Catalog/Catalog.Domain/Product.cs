namespace Luna.Catalog.Domain;

public sealed class Product
{
    private readonly List<ProductImage> _images = [];

    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public decimal CurrentPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Category? Category { get; set; }
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public ProductImage AddImage(string imageUrl, string altText, int displayOrder, Guid? id = null)
    {
        var image = new ProductImage(id ?? Guid.NewGuid(), Id, imageUrl, altText, displayOrder, this);
        _images.Add(image);
        return image;
    }
}
