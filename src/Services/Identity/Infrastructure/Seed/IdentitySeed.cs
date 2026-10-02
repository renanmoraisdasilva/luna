using Luna.Identity.Application;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

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

    public static async Task SeedServiceClientsAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var clientSecret = configuration["ServiceAuthentication:Clients:Orders:Secret"];
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("The Orders service client secret is not configured.");
        }

        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        if (await applicationManager.FindByClientIdAsync(LunaServiceClients.Orders, cancellationToken) is null)
        {
            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = LunaServiceClients.Orders,
                ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                DisplayName = "Orders service",
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Prefixes.Scope + LunaServiceScopes.InventoryReservationsWrite,
                    OpenIddictConstants.Permissions.Prefixes.Scope + LunaServiceScopes.PaymentsAuthorize,
                    OpenIddictConstants.Permissions.Prefixes.Scope + LunaServiceScopes.ShippingShipmentsWrite,
                },
            }, cancellationToken);
        }

        await SeedStorefrontClientAsync(applicationManager, cancellationToken);

        var scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var scope in new[]
        {
            LunaServiceScopes.InventoryReservationsWrite,
            LunaServiceScopes.PaymentsAuthorize,
            LunaServiceScopes.ShippingShipmentsWrite,
        })
        {
            if (await scopeManager.FindByNameAsync(scope, cancellationToken) is null)
            {
                await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    Name = scope,
                    DisplayName = scope,
                    Resources = { LunaAuthentication.ApiResource },
                }, cancellationToken);
            }
        }
    }

    private static async Task SeedStorefrontClientAsync(
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        if (await applicationManager.FindByClientIdAsync(LunaPublicClients.Storefront, cancellationToken) is not null)
        {
            return;
        }

        await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = LunaPublicClients.Storefront,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "Luna storefront",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            },
        }, cancellationToken);
    }
}
