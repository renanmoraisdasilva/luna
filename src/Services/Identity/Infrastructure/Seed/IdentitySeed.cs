using Luna.Identity.Application;
using Microsoft.AspNetCore.Identity;

namespace Luna.Identity.Infrastructure.Seed;

public static class IdentitySeed
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in IdentityRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Unable to seed identity role '{roleName}': {errors}");
            }
        }
    }
}