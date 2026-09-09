namespace Luna.Shipping.Api.Middleware;

public sealed class ShippingExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ShippingExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (KeyNotFoundException exception)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = "SHIPPING_RESOURCE_NOT_FOUND", message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = "INVALID_SHIPPING_REQUEST", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = "SHIPPING_CONFLICT", message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled shipping request exception");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." });
            }
        }
    }
}
