using Inventory.Domain.Exceptions;
using System.Text.Json;

namespace Inventory.Api.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<ErrorHandlingMiddleware> logger)
    {
        try
        {
            await next(context);
        }
        catch (InventoryException exception)
        {
            ApiLog.RequestRejected(logger, exception.Code, exception.Message);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = exception.StatusCode;
                context.Response.ContentType = "application/problem+json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    new
                    {
                        type = "https://httpstatuses.com/" + exception.StatusCode,
                        title = exception.Code,
                        status = exception.StatusCode,
                        detail = exception.Message,
                        code = exception.Code,
                        traceId = context.TraceIdentifier
                    },
                    cancellationToken: context.RequestAborted);
            }
        }
        catch (Exception exception)
        {
            ApiLog.UnhandledException(logger, exception);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    new
                    {
                        type = "https://httpstatuses.com/500",
                        title = "internal_server_error",
                        status = StatusCodes.Status500InternalServerError,
                        detail = "An unexpected error occurred.",
                        traceId = context.TraceIdentifier
                    },
                    cancellationToken: context.RequestAborted);
            }
        }
    }
}
