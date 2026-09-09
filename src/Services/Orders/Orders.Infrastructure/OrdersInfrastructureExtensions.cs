using Luna.Orders.Application.Carts;
using Luna.Orders.Application.Checkout;
using Luna.Orders.Application.Orders;
using Luna.Orders.Infrastructure.Checkout;
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
        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenHandler>();
        services.AddHttpClient<ICatalogCheckoutClient, CatalogCheckoutClient>(client => client.BaseAddress = GetServiceUri(configuration, "CatalogApi"))
            .AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IShippingCheckoutClient, ShippingCheckoutClient>(client => client.BaseAddress = GetServiceUri(configuration, "ShippingApi"))
            .AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IInventoryCheckoutClient, InventoryCheckoutClient>(client => client.BaseAddress = GetServiceUri(configuration, "InventoryApi"))
            .AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IPaymentsCheckoutClient, PaymentsCheckoutClient>(client => client.BaseAddress = GetServiceUri(configuration, "PaymentsApi"))
            .AddHttpMessageHandler<BearerTokenHandler>();
        services.AddScoped<ICartWriteRepository, CartRepository>();
        services.AddScoped<ICartReadRepository, CartReadRepository>();
        services.AddScoped<IOrderWriteRepository, OrderRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<GetCartHandler>();
        services.AddScoped<AddCartItemHandler>();
        services.AddScoped<ChangeCartItemQuantityHandler>();
        services.AddScoped<RemoveCartItemHandler>();
        return services;
    }

    private static Uri GetServiceUri(IConfiguration configuration, string key) =>
        new(configuration[key] ?? throw new InvalidOperationException($"{key} is not configured."));

    public static async Task InitializeOrdersDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
