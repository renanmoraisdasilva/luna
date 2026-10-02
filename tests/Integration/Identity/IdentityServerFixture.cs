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

public sealed class IdentityServerFixture : IAsyncLifetime
{
    public const string DatabasePassword = "Your_password123";
    public const string DatabaseName = "IdentityTests";
    public const string OrdersClientSecret = "integration-orders-client-secret";

    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword(DatabasePassword)
        .Build();

    private Respawner respawner = null!;

    public string SigningKeyPath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"luna-identity-tests-{Guid.NewGuid():N}",
        "signing.pfx");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SigningKeyPath)!);
        await File.WriteAllBytesAsync(SigningKeyPath, CreateSelfSignedCertificate("CN=Luna Identity Tests"));

        await container.StartAsync();

        await using (var connection = new SqlConnection(MasterConnectionString))
        {
            await connection.OpenAsync();
            await using var create = connection.CreateCommand();
            create.CommandText = $"IF DB_ID('{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];";
            await create.ExecuteNonQueryAsync();
        }

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

    public HttpClient CreateClient()
    {
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

    public IdentityServerFactory Factory => factory ??= new IdentityServerFactory(this);

    public async Task ResetAsync()
    {
        await respawner.ResetAsync(ConnectionString);

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

    public Task<HttpResponseMessage> RequestClientCredentialsTokenAsync(
        string clientId,
        string? clientSecret,
        string? scope = null,
        CancellationToken cancellationToken = default) =>
        RequestClientCredentialsTokenAsync(clientId, clientSecret, scope, CreateClient(), cancellationToken);

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

    public Task<HttpResponseMessage> RequestPasswordTokenAsync(
        string email,
        string password,
        string clientId = LunaPublicClients.Storefront,
        CancellationToken cancellationToken = default) =>
        RequestPasswordTokenAsync(email, password, clientId, CreateClient(), cancellationToken);

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
