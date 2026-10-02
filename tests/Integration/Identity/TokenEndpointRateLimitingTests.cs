extern alias IdentityApi;

using System.Net;
using FluentAssertions;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Luna.IntegrationTests.Identity;

/// <summary>
/// Tests that the token endpoint is rate limited.
///
/// The token endpoint is the only unauthenticated surface on the authorization server. Without a limit it is
/// an unlimited credential-guessing oracle: an attacker can submit password grants at whatever rate the
/// network allows and learn from the responses which accounts exist.
/// </summary>
[Collection(IdentityServerCollection.Name)]
public sealed class TokenEndpointRateLimitingTests(IdentityServerFixture fixture)
{
    [Fact]
    public async Task Rejects_token_requests_beyond_the_configured_limit()
    {
        // Three requests per window, so the fourth must be refused. A generous default would make this test
        // slow and brittle, which is why the limit is configuration rather than a constant.
        using var host = new ThrottledIdentityServerFactory(fixture, permitLimit: 3);
        using var client = host.Client;

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var response = await IdentityServerFixture.RequestPasswordTokenAsync(
                "nobody@example.test",
                "wrong-password",
                LunaPublicClients.Storefront,
                client);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(3).Should().OnlyContain(status => status != HttpStatusCode.TooManyRequests,
            "the configured budget must be honoured before it is exhausted");

        statuses.Last().Should().Be(
            HttpStatusCode.TooManyRequests,
            "the request after the configured budget is exhausted must be refused");
    }

    [Fact]
    public async Task Does_not_limit_other_endpoints()
    {
        using var host = new ThrottledIdentityServerFactory(fixture, permitLimit: 1);
        using var client = host.Client;

        // Exhaust the token budget, then prove an unrelated endpoint is unaffected. A global limiter that was
        // applied to every path would be a self-inflicted outage on the health checks the orchestrator polls.
        using (var _ = await IdentityServerFixture.RequestPasswordTokenAsync(
            "nobody@example.test",
            "wrong-password",
            LunaPublicClients.Storefront,
            client))
        {
        }

        using (var _ = await IdentityServerFixture.RequestPasswordTokenAsync(
            "nobody@example.test",
            "wrong-password",
            LunaPublicClients.Storefront,
            client))
        {
        }

        using var health = await client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);

        using var registration = await client.GetAsync("/api/v1/identity/does-not-exist");
        registration.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}

/// <summary>
/// An Identity host with a deliberately small token-endpoint budget so the limiter can be observed without
/// issuing hundreds of requests.
/// </summary>
internal sealed class ThrottledIdentityServerFactory(IdentityServerFixture fixture, int permitLimit)
    : WebApplicationFactory<IdentityApi::IdentityServerEntryPoint>
{
    private static readonly string[] RateLimitVariables = ["Identity__RateLimiting__TokenRequestsPerMinute"];

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

    /// <summary>
    /// The limit is read while the host is being built, which happens before ConfigureAppConfiguration
    /// callbacks run, so it is supplied through the process environment. The Identity collection disables
    /// parallelisation, so this is safe.
    /// </summary>
    public HttpClient Client
    {
        get
        {
            foreach (var variable in RateLimitVariables)
            {
                Environment.SetEnvironmentVariable(variable, permitLimit.ToString());
            }

            Environment.SetEnvironmentVariable("OpenIddict__Keys__SigningKeyPath", fixture.SigningKeyPath);

            try
            {
                return CreateClient();
            }
            finally
            {
                foreach (var variable in RateLimitVariables)
                {
                    Environment.SetEnvironmentVariable(variable, null);
                }

                Environment.SetEnvironmentVariable("OpenIddict__Keys__SigningKeyPath", null);
            }
        }
    }
}
