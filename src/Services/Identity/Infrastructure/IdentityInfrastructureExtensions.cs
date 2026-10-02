using Luna.Identity.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Luna.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static async Task MigrateAndSeedIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        // The DbContext and the role manager are scoped, and the application starts from the root provider.
        // Resolving them from the root works in Production because scope validation is off there, and throws in
        // Development where it is on, so the scope is created explicitly as the other services do.
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<LunaIdentityDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await IdentitySeed.SeedRolesAsync(roleManager);
        await IdentitySeed.SeedServiceClientsAsync(
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<IConfiguration>(),
            cancellationToken);
    }
}
