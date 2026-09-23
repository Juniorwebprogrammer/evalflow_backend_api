using evalflow_backend_api.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace evalflow_backend_api.Tests.Infrastructure.Logging;

public class RequestLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestLoggingMiddleware>> _logger = new();

    private static DefaultHttpContext CreateContext(string method, string path, string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private void VerifyLogged(LogLevel level, Func<string, bool> messageMatches)
    {
        _logger.Verify(l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => messageMatches(state.ToString()!)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK, LogLevel.Information)]
    [InlineData(StatusCodes.Status204NoContent, LogLevel.Information)]
    [InlineData(StatusCodes.Status404NotFound, LogLevel.Warning)]
    [InlineData(StatusCodes.Status401Unauthorized, LogLevel.Warning)]
    [InlineData(StatusCodes.Status503ServiceUnavailable, LogLevel.Error)]
    public async Task InvokeAsync_LogsMethodPathAndStatusWithLevelByStatus(int statusCode, LogLevel expectedLevel)
    {
        var context = CreateContext("GET", "/evaluation-cycles/1/comparisons");
        var sut = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }, _logger.Object);

        await sut.InvokeAsync(context);

        VerifyLogged(expectedLevel, message =>
            message.StartsWith($"GET /evaluation-cycles/1/comparisons {statusCode} ") && message.EndsWith(" ms"));
    }

    [Fact]
    public async Task InvokeAsync_DoesNotLogQueryString()
    {
        var context = CreateContext("GET", "/hubs/dashboard", "?access_token=secret-jwt");
        var sut = new RequestLoggingMiddleware(_ => Task.CompletedTask, _logger.Object);

        await sut.InvokeAsync(context);

        VerifyLogged(LogLevel.Information, message =>
            message.StartsWith("GET /hubs/dashboard 200 ") && !message.Contains("secret-jwt"));
    }

    [Fact]
    public async Task InvokeAsync_WhenPipelineThrows_LogsServerErrorAndRethrows()
    {
        var context = CreateContext("POST", "/auth/forgot-password");
        var sut = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("boom"), _logger.Object);

        var act = () => sut.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyLogged(LogLevel.Error, message => message.StartsWith("POST /auth/forgot-password 500 "));
    }
}
