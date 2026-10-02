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

                UnauthenticatedCustomerException =>
                    (StatusCodes.Status401Unauthorized,
                        new ApiError(UnauthenticatedCustomerException.ErrorCode, exception.Message)),

                ArgumentException => (StatusCodes.Status400BadRequest, new ApiError("INVALID_REQUEST", exception.Message)),
                KeyNotFoundException => (StatusCodes.Status404NotFound, new ApiError("CART_NOT_FOUND", exception.Message)),

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
