using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Luna.Orders.Infrastructure.Checkout;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Luna.IntegrationTests.Orders;

public sealed class BearerTokenHandlerTests
{
    [Fact]
    public async Task Forwards_the_inbound_authorization_header_to_the_upstream_request()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer inner-token";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        using var inner = new CaptureHandler();
        using var client = new HttpClient(new BearerTokenHandler(accessor) { InnerHandler = inner });

        using var response = await client.GetAsync("http://localhost/api/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Authorization.Should().NotBeNull();
        inner.Authorization!.Scheme.Should().Be("Bearer");
        inner.Authorization.Parameter.Should().Be("inner-token");
    }

    [Fact]
    public async Task Sends_no_authorization_header_without_an_http_context_or_valid_header_value()
    {
        var accessor = new HttpContextAccessor();
        using var inner = new CaptureHandler();
        using var client = new HttpClient(new BearerTokenHandler(accessor) { InnerHandler = inner });

        using var response = await client.GetAsync("http://localhost/api/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Leaves_an_unparseable_authorization_header_out_of_the_upstream_request()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = ":not-a-scheme";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        using var inner = new CaptureHandler();
        using var client = new HttpClient(new BearerTokenHandler(accessor) { InnerHandler = inner });

        using var response = await client.GetAsync("http://localhost/api/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Authorization.Should().BeNull();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
