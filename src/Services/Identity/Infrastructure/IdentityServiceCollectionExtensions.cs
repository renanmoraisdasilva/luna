using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Luna.Identity.Infrastructure;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDbContext<LunaIdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Database")));

        services.AddIdentityCore<LunaUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<LunaIdentityDbContext>()
            .AddSignInManager();

        services.Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
        {
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        });

        services.AddTokenEndpointRateLimiting(configuration);

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
                options.AllowClientCredentialsFlow();
                options.DisableAccessTokenEncryption();
                ConfigureKeys(options, configuration, environment);

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

    private static void ConfigureKeys(
        OpenIddictServerBuilder options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate();
            options.AddDevelopmentSigningCertificate();
            return;
        }

        var keys = configuration.GetSection(OpenIddictKeyOptions.SectionName).Get<OpenIddictKeyOptions>();
        if (string.IsNullOrWhiteSpace(keys?.SigningKeyPath))
        {
            throw new InvalidOperationException(
                $"{OpenIddictKeyOptions.SectionName}:SigningKeyPath must be configured when the environment is " +
                $"'{environment.EnvironmentName}'. The authorization server cannot fall back to a development " +
                "certificate outside Development, because that certificate is not persisted and would change on " +
                "every restart, invalidating all issued tokens. Generate one with: " +
                "dotnet dev-certs export -k -p <path> (or supply a PKCS#12 or PEM certificate).");
        }

        options.AddSigningCertificate(
            OpenIddictKeyLoader.Load(keys.SigningKeyPath, keys.Passphrase, "signing"));

        if (!string.IsNullOrWhiteSpace(keys.EncryptionKeyPath))
        {
            options.AddEncryptionCertificate(
                OpenIddictKeyLoader.Load(keys.EncryptionKeyPath, keys.Passphrase, "encryption"));
        }
        else
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Luna OpenIddict Encryption",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            options.AddEncryptionCertificate(
                request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow.AddYears(1)));
        }
    }
}
