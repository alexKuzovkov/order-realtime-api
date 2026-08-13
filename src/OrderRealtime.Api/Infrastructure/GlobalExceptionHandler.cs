using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Infrastructure;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            IdempotencyConflictException => StatusCodes.Status409Conflict,
            BusinessRuleValidationException or DomainValidationException =>
                StatusCodes.Status422UnprocessableEntity,
            OperationCanceledException => 499,
            _ => StatusCodes.Status500InternalServerError
        };

        logger.Log(
            status >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception,
            "Request failed with status {StatusCode}; TraceId: {TraceId}",
            status,
            context.TraceIdentifier);

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    401 => "Authentication required",
                    409 => "Idempotency conflict",
                    422 => "Business validation failed",
                    _ => "Request failed"
                },
                Detail = status >= 500
                    ? "An unexpected error occurred."
                    : exception.Message,
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
    }
}
