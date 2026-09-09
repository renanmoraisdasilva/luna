using Luna.Contracts.Correlation;
using Serilog.Context;

namespace Luna.Payments.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeaders.CorrelationId].FirstOrDefault()
            ?? Guid.NewGuid().ToString("D");
        context.Response.Headers[CorrelationHeaders.CorrelationId] = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
