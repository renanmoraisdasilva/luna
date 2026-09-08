using Luna.Inventory.Application.Reservations;
using Luna.Inventory.Infrastructure.Repositories;
using Luna.Inventory.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luna.Inventory.Infrastructure;

public static class InventoryInfrastructureExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        return services;
    }

    public static async Task InitializeInventoryDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await InventorySeed.SeedAsync(db, cancellationToken);
    }
}
