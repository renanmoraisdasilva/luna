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
