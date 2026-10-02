using System.Threading.RateLimiting;
using Luna.Contracts.Authentication;
using OpenIddict.Abstractions;

namespace Luna.Identity.Infrastructure;

public static class IdentityRateLimitingExtensions
{
    public const string ConfigurationSection = "Identity:RateLimiting";

    public const string TokenEndpointPath = "/api/v1/identity/connect/token";

    public static IServiceCollection AddTokenEndpointRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSection);
        var permitLimit = Math.Max(1, section.GetValue<int?>("TokenRequestsPerMinute") ?? 10);
        var window = section.GetValue<TimeSpan?>("Window") ?? TimeSpan.FromMinutes(1);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsTokenEndpoint(context)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ResolvePartitionKey(context),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = window,
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter(LunaServiceClients.NotLimited));
        });

        return services;
    }

    private static bool IsTokenEndpoint(HttpContext context) =>
        context.Request.Path.Equals(TokenEndpointPath, StringComparison.OrdinalIgnoreCase);

    private static string ResolvePartitionKey(HttpContext context)
    {
        var clientId = context.User.FindFirst(OpenIddictConstants.Claims.ClientId)?.Value;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return $"client:{clientId}";
        }

        var address = context.Connection.RemoteIpAddress;
        return address is null ? "unknown" : $"ip:{address}";
    }
}
