using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Luna.Authentication.ServiceAuthentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace Luna.UnitTests.Authentication;

public sealed class ServiceTokenProviderTests
{
    [Fact]
    public async Task Requests_and_caches_a_service_token()
    {
        var handler = new StubHandler([new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { access_token = "service-token", token_type = "Bearer", expires_in = 3600 }),
        }]);
        var provider = CreateProvider(handler);

        var first = await provider.GetTokenAsync("inventory", CancellationToken.None);
        var second = await provider.GetTokenAsync("inventory", CancellationToken.None);

        first.Should().Be("service-token");
        second.Should().Be(first);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Basic");
        handler.FormBodies.Should().ContainSingle().Which.Should().Contain("scope=inventory");
    }

    [Fact]
    public async Task Concurrent_requests_share_the_token_fetch()
    {
        var handler = new StubHandler([new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { access_token = "shared-token", token_type = "Bearer", expires_in = 3600 }),
        }]);
        var provider = CreateProvider(handler);

        var results = await Task.WhenAll(
            provider.GetTokenAsync("inventory", CancellationToken.None),
            provider.GetTokenAsync("inventory", CancellationToken.None));

        results.Should().AllBe("shared-token");
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("", "secret", "http://identity/token")]
    [InlineData("client", "", "http://identity/token")]
    [InlineData("client", "secret", "")]
    public async Task Rejects_incomplete_configuration(string clientId, string secret, string endpoint)
    {
        var provider = CreateProvider(new StubHandler([]), clientId, secret, endpoint);

        await provider.Invoking(value => value.GetTokenAsync("scope", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Service authentication is not fully configured.");
    }

    [Fact]
    public async Task Rejects_failed_identity_responses()
    {
        var provider = CreateProvider(new StubHandler([new HttpResponseMessage(HttpStatusCode.BadGateway)]));

        await provider.Invoking(value => value.GetTokenAsync("scope", CancellationToken.None))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Rejects_empty_token_responses()
    {
        var emptyResponseProvider = CreateProvider(new StubHandler([new HttpResponseMessage(HttpStatusCode.OK)]));
        await emptyResponseProvider.Invoking(value => value.GetTokenAsync("scope", CancellationToken.None))
            .Should().ThrowAsync<System.Text.Json.JsonException>();

        var emptyTokenProvider = CreateProvider(new StubHandler([new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { access_token = "", token_type = "Bearer", expires_in = 3600 }),
        }]));
        await emptyTokenProvider.Invoking(value => value.GetTokenAsync("scope", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Identity returned a service token without an access token.");

        var nullResponseProvider = CreateProvider(new StubHandler([new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<object?>(null),
        }]));
        await nullResponseProvider.Invoking(value => value.GetTokenAsync("scope", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Identity returned an empty service token response.");
    }

    private static ServiceTokenProvider CreateProvider(
        StubHandler handler,
        string clientId = "orders",
        string secret = "secret",
        string endpoint = "http://identity/token")
    {
        var client = new HttpClient(handler);
        return new ServiceTokenProvider(
            new StubHttpClientFactory(client),
            Options.Create(new ServiceAuthenticationOptions
            {
                ClientId = clientId,
                ClientSecret = secret,
                TokenEndpoint = endpoint,
            }));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(IReadOnlyCollection<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> remaining = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> FormBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                FormBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return remaining.Dequeue();
        }
    }
}
