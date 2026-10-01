extern alias IdentityApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luna.IntegrationTests.Identity;

/// <summary>
/// Hosts the real Identity authorization server in memory, pointing the data store at the fixture
/// container and seeding the service clients through the same code path production uses.
/// </summary>
public sealed class IdentityServerFactory(IdentityServerFixture fixture)
    : WebApplicationFactory<IdentityApi::IdentityServerEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The Identity host runs as Production because MigrateAndSeedIdentityAsync resolves a scoped DbContext
        // from the root provider, which ASP.NET Core only permits when scope validation is off. That is how
        // the service runs in the container, so the test mirrors it rather than masking the behaviour with a
        // Development environment. Production also means the configured signing key is required.
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