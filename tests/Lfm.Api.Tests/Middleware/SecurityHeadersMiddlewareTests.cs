using FluentAssertions;
using Lfm.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    /// <summary>
    /// The key used by FunctionContextExtensions.GetHttpContext() to retrieve the
    /// HttpContext from FunctionContext.Items. Defined in the Azure Functions Worker
    /// SDK as an internal constant.
    /// </summary>
    private const string HttpContextKey = "HttpRequestContext";

    private static (SecurityHeadersMiddleware middleware, Mock<FunctionContext> context, DefaultHttpContext httpContext) CreateTestContext()
    {
        var httpContext = new DefaultHttpContext();
        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(items);

        return (new SecurityHeadersMiddleware(), mockContext, httpContext);
    }

    [Fact]
    public async Task Sets_all_security_headers_on_response()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(mockContext.Object, next);

        nextCalled.Should().BeTrue();
        httpContext.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        httpContext.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        httpContext.Response.Headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains");
        httpContext.Response.Headers["Content-Security-Policy"].ToString().Should().Be("default-src 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task Headers_present_when_next_throws()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ => throw new InvalidOperationException("downstream failure");

        var act = () => middleware.Invoke(mockContext.Object, next);

        await act.Should().ThrowAsync<InvalidOperationException>();
        httpContext.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        httpContext.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        httpContext.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        httpContext.Response.Headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains");
        httpContext.Response.Headers["Content-Security-Policy"].ToString().Should().Be("default-src 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task Calls_next_when_http_context_is_null()
    {
        var items = new Dictionary<object, object>();
        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(items);

        var middleware = new SecurityHeadersMiddleware();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(mockContext.Object, next);

        nextCalled.Should().BeTrue();
    }
}
