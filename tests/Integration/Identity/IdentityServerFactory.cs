extern alias IdentityApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luna.IntegrationTests.Identity;

public sealed class IdentityServerFactory(IdentityServerFixture fixture)
    : WebApplicationFactory<IdentityApi::IdentityServerEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The token endpoint limiter defaults to 10 requests per minute per client, which is the production
        // policy these tests should not be subject to. The grant suites share this one host and exceed that
        // budget, so they throttled each other into 429 responses depending on execution order.
        // TokenEndpointRateLimitingTests builds its own host at a low limit, which is where the limiter
        // itself is verified.
        //
        // This has to be an environment variable rather than an in-memory setting below. The limiter reads
        // its permit limit while the Identity services are registered, and that happens before the
        // ConfigureAppConfiguration sources are added, so an in-memory value arrives too late and the host
        // silently falls back to 10. Environment variables are part of the default configuration from the
        // start, so one set here is picked up. Setting it per host also keeps this deterministic regardless
        // of which host happens to be built first.
        Environment.SetEnvironmentVariable("Identity__RateLimiting__TokenRequestsPerMinute", "10000");

        builder.UseEnvironment(Environments.Production);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = fixture.ConnectionString,
                ["OpenIddict:Issuer"] = "http://identity.test/",
                ["ServiceAuthentication:Clients:Orders:Secret"] = IdentityServerFixture.OrdersClientSecret,
            });
        });
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }
}
