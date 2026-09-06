using Luna.Contracts.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;

namespace Luna.Authentication;

public static class LunaAuthenticationDefaults
{
    public const string ValidationScheme = "OpenIddict.Validation.AspNetCore";
}

public static class LunaAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddLunaJwtValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenIddict()
            .AddValidation(options =>
            {
                options.SetIssuer(new Uri(configuration["OpenIddict:Issuer"] ?? "http://localhost:5001/"));
                options.AddAudiences(LunaAuthentication.Audience);
                options.UseSystemNetHttp();
                options.UseAspNetCore();
            });

        return services;
    }
}
