extern alias PaymentsApi;

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentsApi::Luna.Payments.Api.Middleware;
using Xunit;

namespace Luna.IntegrationTests.Payments;

public sealed class PaymentsMiddlewareTests
{
    [Fact]
    public async Task Exception_middleware_maps_payment_errors()
    {
        var cases = new (Exception Exception, int Status, string Code)[]
        {
            (new ArgumentException("bad request"), StatusCodes.Status400BadRequest, "INVALID_PAYMENT_REQUEST"),
            (new InvalidOperationException("conflict"), StatusCodes.Status409Conflict, "PAYMENT_CONFLICT"),
            (new DbUpdateConcurrencyException(), StatusCodes.Status409Conflict, "PAYMENT_CONCURRENCY_CONFLICT"),
        };

        foreach (var testCase in cases)
        {
            var context = new DefaultHttpContext();
            await using var body = new MemoryStream();
            context.Response.Body = body;
            var middleware = new PaymentsExceptionHandlingMiddleware(
                _ => throw testCase.Exception,
                NullLogger<PaymentsExceptionHandlingMiddleware>.Instance);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(testCase.Status);
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body);
            document.RootElement.GetProperty("code").GetString().Should().Be(testCase.Code);
            document.RootElement.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Exception_middleware_returns_a_generic_error_for_unexpected_failures()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var middleware = new PaymentsExceptionHandlingMiddleware(
            _ => throw new Exception("boom"),
            NullLogger<PaymentsExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body);
        document.RootElement.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
    }

    [Fact]
    public async Task Exception_middleware_does_not_write_after_the_response_started()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature(body));
        var middleware = new PaymentsExceptionHandlingMiddleware(
            _ => throw new Exception("boom"),
            NullLogger<PaymentsExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.HasStarted.Should().BeTrue();
        body.Length.Should().Be(0);
    }

    private sealed class StartedResponseFeature(Stream body) : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = body;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
