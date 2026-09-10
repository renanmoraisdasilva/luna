using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Luna.Authentication.ServiceAuthentication;

public sealed class ServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceAuthenticationOptions> options)
{
    private readonly ConcurrentDictionary<string, CachedServiceToken> tokens = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    public async Task<string> GetTokenAsync(string scope, CancellationToken cancellationToken)
    {
        if (tokens.TryGetValue(scope, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cached.AccessToken;
        }

        var tokenLock = locks.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (tokens.TryGetValue(scope, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return cached.AccessToken;
            }

            var settings = options.Value;
            if (string.IsNullOrWhiteSpace(settings.ClientId) ||
                string.IsNullOrWhiteSpace(settings.ClientSecret) ||
                string.IsNullOrWhiteSpace(settings.TokenEndpoint))
            {
                throw new InvalidOperationException("Service authentication is not fully configured.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = scope,
                }),
            };
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Identity returned an empty service token response.");

            if (string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("Identity returned a service token without an access token.");
            }

            cached = new CachedServiceToken(
                token.AccessToken,
                DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn));
            tokens[scope] = cached;
            return cached.AccessToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private sealed record CachedServiceToken(string AccessToken, DateTimeOffset ExpiresAt);

    private sealed record ServiceTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("token_type")]
        string TokenType,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn,
        [property: JsonPropertyName("scope")]
        string? Scope);
}
