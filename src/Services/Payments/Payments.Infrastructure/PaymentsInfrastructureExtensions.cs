using Luna.Payments.Application.Authorization;
using Luna.Payments.Infrastructure.Providers;
using Luna.Payments.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luna.Payments.Infrastructure;

public static class PaymentsInfrastructureExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));
        services.AddScoped<IPaymentWriteRepository, PaymentWriteRepository>();
        services.AddScoped<IPaymentUnitOfWork, PaymentUnitOfWork>();
        services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
        return services;
    }

    public static async Task InitializePaymentsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
