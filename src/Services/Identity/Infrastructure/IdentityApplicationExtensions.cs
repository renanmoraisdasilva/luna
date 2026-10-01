using Luna.Contracts.Correlation;
using Serilog;
using Serilog.Events;
using Serilog.Context;

namespace Luna.Identity.Infrastructure;

public static class IdentityApplicationExtensions
{
    public static WebApplication UseIdentityPipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = (httpContext, _, exception) => httpContext.Request.Path == "/health" ? LogEventLevel.Verbose : exception is not null ? LogEventLevel.Error : LogEventLevel.Information);

        if (app.Environment.IsDevelopment())
        {
            // The OpenAPI document is development-only. Publishing it would disclose the token endpoint, the
            // registered client identifiers and the flows the authorization server supports.
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCorrelationHandling();

        return app;
    }

    private static IApplicationBuilder UseCorrelationHandling(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[CorrelationHeaders.CorrelationId].FirstOrDefault()
                ?? Guid.NewGuid().ToString("D");
            context.Response.Headers[CorrelationHeaders.CorrelationId] = correlationId;
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                try
                {
                    await next();
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Unhandled request exception");
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            code = "INTERNAL_ERROR",
                            message = "An unexpected error occurred.",
                            correlationId,
                        });
                    }
                }
            }
        });
    }
}
