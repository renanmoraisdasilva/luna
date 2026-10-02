using System.Threading.RateLimiting;
using Luna.Contracts.Authentication;
using OpenIddict.Abstractions;

namespace Luna.Identity.Infrastructure;

/// <summary>
/// Rate limiting for the token endpoint.
/// </summary>
/// <remarks>
/// The token endpoint is the only unauthenticated surface on the authorization server: it accepts a password
/// grant, so an unthrottled caller can attempt unlimited credential guesses and unlimited service-token
/// requests. Both are limited here, by client address, using the built-in ASP.NET Core rate limiter.
/// <para>
/// The limiter is a global limiter with a per-request partition rather than endpoint metadata, because the
/// token endpoint is served by OpenIddict's own middleware rather than by a mapped MVC endpoint, so it carries
/// no endpoint metadata to attach a limiter to. Every other path is explicitly given no limiter.
/// </para>
/// </remarks>
public static class IdentityRateLimitingExtensions
{
    public const string ConfigurationSection = "Identity:RateLimiting";

    /// <summary>The token endpoint path, relative to the Identity host.</summary>
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

    /// <summary>
    /// The partition key is the caller's address. It falls back to the identity claim when the request is
    /// already authenticated, so that several callers behind one address share a budget rather than one caller
    /// being able to exhaust it.
    /// </summary>
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
