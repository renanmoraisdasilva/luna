namespace Luna.Catalog.Domain;

public sealed class Category
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public List<Product> Products { get; set; } = [];
}
