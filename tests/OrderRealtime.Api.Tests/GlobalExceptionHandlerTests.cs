using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OrderRealtime.Api.Infrastructure;
using OrderRealtime.Api.Orders;

namespace OrderRealtime.Api.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(IdempotencyConflictException), StatusCodes.Status409Conflict)]
    [InlineData(typeof(BusinessRuleValidationException), StatusCodes.Status422UnprocessableEntity)]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized)]
    public async Task TryHandleAsync_MapsKnownExceptions(Type exceptionType, int expectedStatus)
    {
        ProblemDetails? written = null;
        var writer = Substitute.For<IProblemDetailsService>();
        writer.TryWriteAsync(Arg.Do<ProblemDetailsContext>(context =>
                written = context.ProblemDetails))
            .Returns(new ValueTask<bool>(true));
        var handler = new GlobalExceptionHandler(
            writer, NullLogger<GlobalExceptionHandler>.Instance);
        var exception = CreateException(exceptionType);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(expectedStatus, httpContext.Response.StatusCode);
        Assert.Equal(expectedStatus, written?.Status);
        Assert.True(written?.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotExposeUnexpectedExceptionDetails()
    {
        ProblemDetails? written = null;
        var writer = Substitute.For<IProblemDetailsService>();
        writer.TryWriteAsync(Arg.Do<ProblemDetailsContext>(context =>
                written = context.ProblemDetails))
            .Returns(new ValueTask<bool>(true));
        var handler = new GlobalExceptionHandler(
            writer, NullLogger<GlobalExceptionHandler>.Instance);

        await handler.TryHandleAsync(
            new DefaultHttpContext(),
            new InvalidOperationException("sensitive database details"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, written?.Status);
        Assert.DoesNotContain("sensitive", written?.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static Exception CreateException(Type type) => type.Name switch
    {
        nameof(IdempotencyConflictException) => new IdempotencyConflictException("key"),
        nameof(BusinessRuleValidationException) => new BusinessRuleValidationException("invalid"),
        nameof(UnauthorizedAccessException) => new UnauthorizedAccessException("unauthorized"),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
