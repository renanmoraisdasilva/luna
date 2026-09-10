using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Luna.Authentication.ServiceAuthentication;

public static class ServiceAuthenticationExtensions
{
    public static IServiceCollection AddLunaServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServiceAuthenticationOptions>(configuration.GetSection("ServiceAuthentication"));
        services.AddHttpClient();
        services.AddSingleton<ServiceTokenProvider>();
        return services;
    }

    public static AuthorizationPolicyBuilder RequireLunaService(
        this AuthorizationPolicyBuilder builder,
        string clientId,
        string scope)
    {
        return builder
            .AddAuthenticationSchemes(LunaAuthenticationDefaults.ValidationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                context.User.HasClaim(OpenIddictConstants.Claims.ClientId, clientId) &&
                context.User.Claims
                    .Where(claim => claim.Type == OpenIddictConstants.Claims.Scope)
                    .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Contains(scope, StringComparer.Ordinal));
    }
}

public static class LunaServicePolicies
{
    public const string OrdersInventoryReservationsWrite = "orders-inventory-reservations-write";
    public const string OrdersPaymentsAuthorize = "orders-payments-authorize";
    public const string OrdersShippingShipmentsWrite = "orders-shipping-shipments-write";
}
