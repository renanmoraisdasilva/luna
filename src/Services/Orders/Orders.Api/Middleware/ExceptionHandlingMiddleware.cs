using Luna.Contracts.Errors;
using Luna.Orders.Application.Checkout;
using Microsoft.EntityFrameworkCore;

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
                CheckoutRejectedException rejected => (StatusCodes.Status422UnprocessableEntity, new ApiError(rejected.Code, rejected.Message)),
                ArgumentException => (StatusCodes.Status400BadRequest, new ApiError("INVALID_REQUEST", exception.Message)),
                KeyNotFoundException => (StatusCodes.Status404NotFound, new ApiError("CART_NOT_FOUND", exception.Message)),

                // A concurrency conflict means another operator or request advanced the order between the read
                // and the write. It is a distinct condition from a state-machine violation and gets its own code
                // so a client can retry rather than treat it as a permanent rejection.
                DbUpdateConcurrencyException =>
                    (StatusCodes.Status409Conflict,
                        new ApiError("ORDER_CONCURRENCY_CONFLICT", "The order was modified concurrently. Retry the request.")),

                InvalidOperationException => (StatusCodes.Status409Conflict, new ApiError("FULFILLMENT_CONFLICT", exception.Message)),
                _ => (StatusCodes.Status500InternalServerError, new ApiError("INTERNAL_ERROR", "An unexpected error occurred.")),
            };

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(error);
        }
    }
}
