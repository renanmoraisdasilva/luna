using Luna.Catalog.Application.Categories;
using Luna.Catalog.Application.Products;
using Luna.Catalog.Infrastructure.Database;
using Luna.Catalog.Infrastructure.Repositories;
using Luna.Catalog.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Luna.Catalog.Infrastructure;

public static class CatalogInfrastructureExtensions
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Database")));
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
        services.AddScoped<GetProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<GetCategoriesHandler>();
        return services;
    }

    public static async Task InitializeCatalogDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await CatalogSeed.SeedAsync(db, cancellationToken);
    }
}
