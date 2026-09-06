using Luna.Contracts.Errors;

namespace Luna.Orders.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request exception");

            if (context.Response.HasStarted)
            {
                return;
            }

            var (statusCode, error) = exception switch
            {
                ArgumentException => (StatusCodes.Status400BadRequest, new ApiError("INVALID_REQUEST", exception.Message)),
                KeyNotFoundException => (StatusCodes.Status404NotFound, new ApiError("CART_NOT_FOUND", exception.Message)),
                _ => (StatusCodes.Status500InternalServerError, new ApiError("INTERNAL_ERROR", "An unexpected error occurred.")),
            };

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(error);
        }
    }
}
