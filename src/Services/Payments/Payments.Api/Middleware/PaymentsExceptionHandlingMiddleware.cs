using Microsoft.EntityFrameworkCore;

namespace Luna.Payments.Api.Middleware;

public sealed class PaymentsExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<PaymentsExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ArgumentException exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = "INVALID_PAYMENT_REQUEST", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = "PAYMENT_CONFLICT", message = exception.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = "PAYMENT_CONCURRENCY_CONFLICT", message = "The payment was modified concurrently. Retry the authorization request." });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled payment request exception");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." });
            }
        }
    }
}
