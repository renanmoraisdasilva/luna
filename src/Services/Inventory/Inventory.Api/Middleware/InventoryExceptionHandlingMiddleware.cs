using Luna.Inventory.Domain;

namespace Luna.Inventory.Api.Middleware;

public sealed class InventoryExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<InventoryExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (InsufficientInventoryException exception)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = "INSUFFICIENT_INVENTORY", message = exception.Message });
        }
        catch (StockNotFoundException exception)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = "STOCK_NOT_FOUND", message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = "RESERVATION_NOT_FOUND", message = exception.Message });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled inventory request exception");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." });
            }
        }
    }
}
