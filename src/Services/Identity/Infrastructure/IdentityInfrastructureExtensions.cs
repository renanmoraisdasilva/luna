using Luna.Identity.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Luna.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static async Task MigrateAndSeedIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var dbContext = services.GetRequiredService<LunaIdentityDbContext>();
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await IdentitySeed.SeedRolesAsync(roleManager);
        await IdentitySeed.SeedServiceClientsAsync(
            services,
            services.GetRequiredService<IConfiguration>(),
            cancellationToken);
    }
}
