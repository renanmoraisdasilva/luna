using System.Text.Json;
using FluentAssertions;
using Luna.Contracts.Errors;
using Luna.Orders.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Luna.IntegrationTests.Orders;

public sealed class MiddlewareTests
{
    [Fact]
    public async Task Exception_middleware_maps_known_and_unknown_errors()
    {
        var cases = new (Exception Exception, int Status, string Code)[]
        {
            (new ArgumentException("bad request"), StatusCodes.Status400BadRequest, "INVALID_REQUEST"),
            (new KeyNotFoundException("missing"), StatusCodes.Status404NotFound, "CART_NOT_FOUND"),
            (new InvalidOperationException("unexpected"), StatusCodes.Status409Conflict, "FULFILLMENT_CONFLICT"),

            (new UnauthenticatedCustomerException(), StatusCodes.Status401Unauthorized, "UNAUTHENTICATED"),

            (new DbUpdateConcurrencyException("stale row"), StatusCodes.Status409Conflict, "ORDER_CONCURRENCY_CONFLICT"),
        };

        foreach (var testCase in cases)
        {
            var context = new DefaultHttpContext();
            await using var body = new MemoryStream();
            context.Response.Body = body;
            var middleware = new ExceptionHandlingMiddleware(
                _ => throw testCase.Exception,
                NullLogger<ExceptionHandlingMiddleware>.Instance);

            await middleware.InvokeAsync(context);

            context.Response.StatusCode.Should().Be(testCase.Status);
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body);
            document.RootElement.GetProperty("code").GetString().Should().Be(testCase.Code);
        }
    }

    [Fact]
    public async Task Exception_middleware_does_not_write_after_response_started()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature(body));
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("unexpected"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.HasStarted.Should().BeTrue();
        body.Length.Should().Be(0);
    }

    [Fact]
    public async Task Correlation_middleware_preserves_or_generates_a_correlation_id()
    {
        var suppliedContext = new DefaultHttpContext();
        suppliedContext.Request.Headers["X-Correlation-ID"] = "provided-id";
        var supplied = new CorrelationIdMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        await supplied.InvokeAsync(suppliedContext);

        suppliedContext.Response.Headers["X-Correlation-ID"].ToString().Should().Be("provided-id");

        var generatedContext = new DefaultHttpContext();
        var generated = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await generated.InvokeAsync(generatedContext);

        Guid.TryParse(generatedContext.Response.Headers["X-Correlation-ID"].ToString(), out _).Should().BeTrue();
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
