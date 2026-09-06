using Luna.Orders.Application.Carts;
using Luna.Orders.Infrastructure.Database;
using Luna.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luna.Orders.Infrastructure;

public static class OrdersInfrastructureExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("Database")));
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<CartService>();
        return services;
    }

    public static async Task InitializeOrdersDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
