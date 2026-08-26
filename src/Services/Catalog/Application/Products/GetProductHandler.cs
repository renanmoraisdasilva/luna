namespace Luna.Catalog.Application.Products;

public sealed class GetProductHandler(IProductReadRepository repository)
{
    public async Task<ProductResponse?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetActiveProductAsync(id, cancellationToken);
    }
}
