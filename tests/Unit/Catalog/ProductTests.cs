using FluentAssertions;
using Luna.Catalog.Domain;
using Xunit;

namespace Luna.UnitTests.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Add_image_adds_a_product_image_with_the_product_reference()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-1",
            Name = "Keyboard",
            Description = "Mechanical keyboard",
        };

        var image = product.AddImage("keyboard.jpg", "Keyboard", 1);

        product.Images.Should().ContainSingle().Which.Should().BeSameAs(image);
        image.ProductId.Should().Be(product.Id);
    }

    [Fact]
    public void Add_image_preserves_an_explicit_id()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-1",
            Name = "Keyboard",
            Description = "Mechanical keyboard",
        };
        var imageId = Guid.NewGuid();

        var image = product.AddImage("keyboard.jpg", "Keyboard", 1, imageId);

        image.Id.Should().Be(imageId);
    }
}
