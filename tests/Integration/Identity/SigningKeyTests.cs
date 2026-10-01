extern alias IdentityApi;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Luna.Contracts.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Xunit;

namespace Luna.IntegrationTests.Identity;

/// <summary>
/// Tests how the authorization server sources the keys it signs tokens with.
///
/// The defect this pins down is that the development certificate is ephemeral: it is not persisted, so every
/// Identity restart generates a different key. That invalidates every outstanding access token and every other
/// service's cached JWKS, turning a routine restart into a fleet-wide authentication outage. Outside
/// Development the server must require an explicitly configured key and refuse to start without one.
/// </summary>
public sealed class SigningKeyTests(IdentityServerFixture fixture) : IClassFixture<IdentityServerFixture>
{
    [Fact]
    public async Task Uses_the_configured_signing_key_when_one_is_supplied()
    {
        using var host = new KeyedIdentityServerFactory(
            fixture,
            signingKeyPath: fixture.SigningKeyPath,
            encryptionKeyPath: null);

        using var response = await RequestServiceTokenAsync(host);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = await ReadAccessTokenAsync(response);
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refuses_to_start_outside_development_without_a_configured_signing_key()
    {
        // An unset key must fail loudly at startup. Falling back to the development certificate would produce a
        // running service whose tokens break on every restart, which is far harder to diagnose than a container
        // that will not start.
        using var host = new KeyedIdentityServerFactory(fixture, signingKeyPath: null, encryptionKeyPath: null);

        var act = () => host.Client;

        act.Should().Throw<Exception>().Where(
            exception => exception.ToString().Contains("SigningKeyPath", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refuses_to_start_when_the_configured_signing_key_is_missing_from_disk()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"luna-missing-{Guid.NewGuid():N}", "absent.pfx");

        using var host = new KeyedIdentityServerFactory(fixture, missingPath, encryptionKeyPath: null);

        var act = () => host.Client;

        act.Should().Throw<Exception>().Where(
            exception => exception.ToString().Contains("signing key was not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Accepts_a_configured_encryption_key()
    {
        var encryptionKeyPath = Path.Combine(
            Path.GetDirectoryName(fixture.SigningKeyPath)!,
            $"encryption-{Guid.NewGuid():N}.pfx");

        await File.WriteAllBytesAsync(
            encryptionKeyPath,
            IdentityServerFixture.CreateSelfSignedCertificate("CN=Luna Identity Tests Encryption"));

        try
        {
            using var host = new KeyedIdentityServerFactory(
                fixture,
                fixture.SigningKeyPath,
                encryptionKeyPath);

            using var response = await RequestServiceTokenAsync(host);

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                "a configured encryption key must not prevent the server issuing tokens");
        }
        finally
        {
            File.Delete(encryptionKeyPath);
        }
    }

    [Fact]
    public async Task Issues_tokens_verifiable_by_a_host_started_afterwards()
    {
        // The point of a configured key: a second host, standing in for a restart or another replica, signs
        // tokens with the same key, so tokens survive a restart. Under an ephemeral development certificate
        // every restart would produce a different signing key and invalidate everything already issued.
        using var first = new KeyedIdentityServerFactory(fixture, fixture.SigningKeyPath, encryptionKeyPath: null);
        using var firstResponse = await RequestServiceTokenAsync(first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var second = new KeyedIdentityServerFactory(fixture, fixture.SigningKeyPath, encryptionKeyPath: null);
        using var secondResponse = await RequestServiceTokenAsync(second);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var claims = IdentityServerFixture.ReadAccessTokenClaims((await ReadAccessTokenAsync(secondResponse))!);

        // A token signed by the configured key decodes as a JWS carrying the shared API audience, which is what
        // the resource services validate against. The issuer is read at registration time from appsettings.json,
        // so only its presence is asserted here.
        claims.Should().ContainKey("iss");
        claims["iss"].GetString().Should().NotBeNullOrWhiteSpace();
        claims.Should().ContainKey("aud");
        claims["aud"].GetString().Should().Be(LunaAuthentication.Audience);
        claims[OpenIddictConstants.Claims.ClientId].GetString().Should().Be(LunaServiceClients.Orders);
    }

    private static Task<HttpResponseMessage> RequestServiceTokenAsync(KeyedIdentityServerFactory host) =>
        IdentityServerFixture.RequestClientCredentialsTokenAsync(
            LunaServiceClients.Orders,
            IdentityServerFixture.OrdersClientSecret,
            LunaServiceScopes.PaymentsAuthorize,
            host.Client);

    private static async Task<string?> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("access_token", out var token) ? token.GetString() : null;
    }
}

/// <summary>
/// An Identity host with an explicit signing key, so each test can supply its own key path or none at all.
/// </summary>
/// <remarks>
/// Production is used deliberately: the scoped <c>DbContext</c> resolved during startup is only permitted when
/// scope validation is off, which is how the container runs the service, and Production is also what makes the
/// configured signing key mandatory.
/// <para>
/// The key paths are supplied as environment variables rather than through <c>ConfigureAppConfiguration</c>.
/// The service registers its OpenIddict keys while the host is being built, which happens before
/// <c>ConfigureAppConfiguration</c> callbacks run, so an in-memory source added there is not visible yet.
/// Environment variables are read by the host builder before that point. The collection disables
/// parallelisation, so mutating the process environment for the duration of a test is safe.
/// </para>
/// </remarks>
internal sealed class KeyedIdentityServerFactory(
    IdentityServerFixture fixture,
    string? signingKeyPath,
    string? encryptionKeyPath)
    : WebApplicationFactory<IdentityApi::IdentityServerEntryPoint>
{
    private static readonly string[] KeyVariables =
    [
        "OpenIddict__Keys__SigningKeyPath",
        "OpenIddict__Keys__EncryptionKeyPath",
    ];

    private HttpClient? client;

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
    /// A client for this host. The environment variables are applied immediately before the host starts and
    /// cleared once it is built, which is what lets the fail-fast tests observe the startup exception.
    /// </summary>
    public HttpClient Client => client ??= CreateClientWithKeys();

    private HttpClient CreateClientWithKeys()
    {
        foreach (var variable in KeyVariables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        Environment.SetEnvironmentVariable("OpenIddict__Keys__SigningKeyPath", signingKeyPath);
        Environment.SetEnvironmentVariable("OpenIddict__Keys__EncryptionKeyPath", encryptionKeyPath);

        try
        {
            return CreateClient();
        }
        finally
        {
            foreach (var variable in KeyVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            client?.Dispose();
        }

        base.Dispose(disposing);
    }
}