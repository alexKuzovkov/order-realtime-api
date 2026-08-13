using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OrderRealtime.Api.Infrastructure;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(IdempotencyConflictException), StatusCodes.Status409Conflict)]
    [InlineData(typeof(BusinessRuleValidationException), StatusCodes.Status422UnprocessableEntity)]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized)]
    public async Task TryHandleAsync_MapsKnownExceptions(
        Type exceptionType,
        int expectedStatus)
    {
        var writer = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            writer, NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            CreateException(exceptionType),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        Assert.Equal(expectedStatus, writer.Written?.Status);
        Assert.True(writer.Written?.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotExposeUnexpectedExceptionDetails()
    {
        var writer = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            writer, NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new InvalidOperationException("sensitive database details"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, writer.Written?.Status);
        Assert.DoesNotContain(
            "sensitive", writer.Written?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static Exception CreateException(Type type) => type.Name switch
    {
        nameof(IdempotencyConflictException) => new IdempotencyConflictException("key"),
        nameof(BusinessRuleValidationException) =>
            new BusinessRuleValidationException("invalid"),
        nameof(UnauthorizedAccessException) =>
            new UnauthorizedAccessException("unauthorized"),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
