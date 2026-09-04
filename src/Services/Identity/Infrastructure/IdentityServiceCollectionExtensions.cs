using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Luna.Identity.Infrastructure;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<LunaIdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddIdentityCore<LunaUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<LunaIdentityDbContext>()
            .AddSignInManager();

        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<LunaIdentityDbContext>())
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(configuration["OpenIddict:Issuer"] ?? "http://localhost:5001/"));
                options.SetTokenEndpointUris("/api/v1/identity/connect/token");
                options.SetConfigurationEndpointUris("/.well-known/openid-configuration");
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();
                options.AcceptAnonymousClients();
                options.DisableAccessTokenEncryption();
                options.AddDevelopmentEncryptionCertificate();
                options.AddDevelopmentSigningCertificate();
                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}