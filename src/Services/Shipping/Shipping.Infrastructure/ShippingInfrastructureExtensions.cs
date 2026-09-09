using Luna.Shipping.Application.Quotes;
using Luna.Shipping.Application.Shipments;
using Luna.Shipping.Application.ShippingMethods;
using Luna.Shipping.Infrastructure.Repositories;
using Luna.Shipping.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luna.Shipping.Infrastructure;

public static class ShippingInfrastructureExtensions
{
    public static IServiceCollection AddShippingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ShippingDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));
        services.AddScoped<IShippingMethodReadRepository, ShippingMethodReadRepository>();
        services.AddScoped<IShippingQuoteWriteRepository, ShippingQuoteWriteRepository>();
        services.AddScoped<IShippingQuoteReadRepository, ShippingQuoteReadRepository>();
        services.AddScoped<IShipmentWriteRepository, ShipmentWriteRepository>();
        return services;
    }

    public static async Task InitializeShippingDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await ShippingSeed.SeedAsync(db, cancellationToken);
    }
}
