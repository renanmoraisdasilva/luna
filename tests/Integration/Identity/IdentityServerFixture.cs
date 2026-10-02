using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Luna.Contracts.Authentication;
using Microsoft.Data.SqlClient;
using Respawn;
using Testcontainers.MsSql;
using Xunit;

namespace Luna.IntegrationTests.Identity;

/// <summary>
/// Hosts the real Identity authorization server against a containerised SQL Server so the token endpoint
/// can be exercised over HTTP. The service-to-service client-credentials grant and the password grant are
/// both security boundaries, so they are tested against the real OpenIddict pipeline rather than a fake
/// authentication handler.
/// </summary>
public sealed class IdentityServerFixture : IAsyncLifetime
{
    public const string DatabasePassword = "Your_password123";
    public const string DatabaseName = "IdentityTests";
    public const string OrdersClientSecret = "integration-orders-client-secret";

    private readonly MsSqlContainer container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(DatabasePassword)
        .Build();

    private Respawner respawner = null!;

    /// <summary>
    /// A self-signed certificate written to disk and reused by every host in this fixture. It stands in for a
    /// key mounted from a secret in production: what is under test is that a configured key is loaded from a
    /// path and reused across host starts, not any particular file format.
    /// </summary>
    public string SigningKeyPath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"luna-identity-tests-{Guid.NewGuid():N}",
        "signing.pfx");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SigningKeyPath)!);
        await File.WriteAllBytesAsync(SigningKeyPath, CreateSelfSignedCertificate("CN=Luna Identity Tests"));

        await container.StartAsync();

        // The container starts with only its default database, so the test database must be created before
        // Respawn can inspect it and before the Identity host can migrate it.
        await using (var connection = new SqlConnection(MasterConnectionString))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"IF DB_ID('{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];";
            await create.ExecuteNonQueryAsync();
        }

        // The Identity host migrates and seeds its schema during startup, so the first host is started before
        // Respawn inspects the database.
        using (var warmup = CreateClient())
        {
            await warmup.GetAsync("/health");
        }

        respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    private string MasterConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = "master"
    }.ConnectionString;

    public string ConnectionString => new SqlConnectionStringBuilder(container.GetConnectionString())
    {
        InitialCatalog = DatabaseName
    }.ConnectionString;

    /// <summary>
    /// Creates a client for the Identity host. Each call gets a fresh client so tests cannot leak
    /// cookies or headers into one another.
    /// </summary>
    public HttpClient CreateClient()
    {
        // The Identity service registers its OpenIddict keys while the host is being built, which happens before
        // ConfigureAppConfiguration callbacks run, so the signing key path has to arrive through the process
        // environment. The Identity collection disables parallelisation, so this is safe.
        Environment.SetEnvironmentVariable("OpenIddict__Keys__SigningKeyPath", SigningKeyPath);
        Environment.SetEnvironmentVariable("OpenIddict__Keys__EncryptionKeyPath", null);
        try
        {
            return Factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OpenIddict__Keys__SigningKeyPath", null);
        }
    }

    private IdentityServerFactory? factory;

    /// <summary>
    /// The host is created lazily because seeding of the service clients happens during host startup, which
    /// requires the container to already be running.
    /// </summary>
    public IdentityServerFactory Factory => factory ??= new IdentityServerFactory(this);

    public async Task ResetAsync()
    {
        await respawner.ResetAsync(ConnectionString);

        // The client application is stored in the same database, so a reset removes it. Rebuilding the host
        // re-runs startup, which reseeds the client through the production seeding path.
        Factory.Dispose();
        factory = new IdentityServerFactory(this);
        using var warmup = CreateClient();
        await warmup.GetAsync("/health");
    }

    public async Task DisposeAsync()
    {
        factory?.Dispose();
        await container.DisposeAsync();

        var keyDirectory = Path.GetDirectoryName(SigningKeyPath);
        if (keyDirectory is not null && Directory.Exists(keyDirectory))
        {
            Directory.Delete(keyDirectory, recursive: true);
        }
    }

    /// <summary>
    /// Creates a throwaway certificate. The tests only need material that OpenIddict accepts as a key; which
    /// certificate it is does not affect what they assert.
    /// </summary>
    internal static byte[] CreateSelfSignedCertificate(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: false));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        return certificate.Export(X509ContentType.Pfx);
    }

    /// <summary>
    /// Posts a client-credentials request. A null <paramref name="clientSecret"/> omits the Authorization
    /// header entirely, which is how an unauthenticated caller would attempt the grant.
    /// </summary>
    public Task<HttpResponseMessage> RequestClientCredentialsTokenAsync(
        string clientId,
        string? clientSecret,
        string? scope = null,
        CancellationToken cancellationToken = default) =>
        RequestClientCredentialsTokenAsync(clientId, clientSecret, scope, CreateClient(), cancellationToken);

    /// <summary>
    /// Posts a client-credentials request through a caller-supplied client, so a test can drive a specific
    /// host rather than the fixture's shared one.
    /// </summary>
    public static Task<HttpResponseMessage> RequestClientCredentialsTokenAsync(
        string clientId,
        string? clientSecret,
        string? scope,
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
        if (scope is not null)
        {
            form["scope"] = scope;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/identity/connect/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        if (clientSecret is not null)
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
        else
        {
            form["client_id"] = clientId;
            request.Content = new FormUrlEncodedContent(form);
        }

        return client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Posts a resource owner password grant. The storefront route sends the registered public client
    /// identifier, so that is the default here.
    /// </summary>
    public Task<HttpResponseMessage> RequestPasswordTokenAsync(
        string email,
        string password,
        string clientId = LunaPublicClients.Storefront,
        CancellationToken cancellationToken = default) =>
        RequestPasswordTokenAsync(email, password, clientId, CreateClient(), cancellationToken);

    /// <summary>
    /// Posts a password grant through a caller-supplied client, so a test can drive a specific host rather than
    /// the fixture's shared one.
    /// </summary>
    public static Task<HttpResponseMessage> RequestPasswordTokenAsync(
        string email,
        string password,
        string clientId,
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        return client.PostAsync(
            "/api/v1/identity/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["username"] = email,
                ["password"] = password,
                ["scope"] = "openid",
            }),
            cancellationToken);
    }

    /// <summary>
    /// Reads the claim set from an issued access token.
    ///
    /// Luna runs OpenIddict with <c>DisableAccessTokenEncryption()</c>, so access tokens are signed but not
    /// encrypted and the payload is readable. Reading the payload is therefore the direct way to assert what
    /// the token asserts about its caller, which is the property these tests care about: the <c>client_id</c>
    /// must be the client that authenticated, not a string the caller chose.
    /// </summary>
    public static IDictionary<string, JsonElement> ReadAccessTokenClaims(string accessToken)
    {
        var segments = accessToken.Split('.');
        segments.Should().HaveCount(
            3,
            "access tokens are signed but not encrypted, so they must be three-segment JWS compact serialisations");

        using var document = JsonDocument.Parse(Base64UrlDecode(segments[1]));
        return document.RootElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone(),
            StringComparer.Ordinal);
    }

    private static string Base64UrlDecode(string segment) =>
        Convert.FromBase64String(
            segment.Replace('-', '+').Replace('_', '/').PadRight(
                segment.Length + ((4 - (segment.Length % 4)) % 4),
                '=')) is var padded
            ? System.Text.Encoding.UTF8.GetString(padded)
            : throw new InvalidOperationException("The token segment could not be decoded.");
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdentityServerCollection : ICollectionFixture<IdentityServerFixture>
{
    public const string Name = "Identity authorization server";
}
