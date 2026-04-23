// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Moq;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    /// <summary>
    /// The key used by FunctionContextExtensions.GetHttpContext() to retrieve the
    /// HttpContext from FunctionContext.Items. Defined in the Azure Functions Worker
    /// SDK as an internal constant.
    /// </summary>
    private const string HttpContextKey = "HttpRequestContext";

    private const string DefaultRepositoryUrl = "https://github.com/lfm-org/lfm";

    private static (SecurityHeadersMiddleware middleware, Mock<FunctionContext> context, DefaultHttpContext httpContext) CreateTestContext(
        string? sourceRepositoryUrl = null)
    {
        var httpContext = new DefaultHttpContext();
        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(items);

        var options = MSOptions.Create(new AgplOptions
        {
            SourceRepositoryUrl = sourceRepositoryUrl ?? DefaultRepositoryUrl,
        });

        return (new SecurityHeadersMiddleware(options), mockContext, httpContext);
    }

    [Fact]
    public async Task Sets_all_security_headers_on_response()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(mockContext.Object, next);

        Assert.True(nextCalled);
        Assert.Equal("nosniff", httpContext.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", httpContext.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", httpContext.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("max-age=31536000; includeSubDomains", httpContext.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", httpContext.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task Headers_present_when_next_throws()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ => throw new InvalidOperationException("downstream failure");

        var act = () => middleware.Invoke(mockContext.Object, next);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Equal("nosniff", httpContext.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", httpContext.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", httpContext.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("max-age=31536000; includeSubDomains", httpContext.Response.Headers["Strict-Transport-Security"].ToString());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", httpContext.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task Calls_next_when_http_context_is_null()
    {
        var items = new Dictionary<object, object>();
        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(items);

        var options = MSOptions.Create(new AgplOptions { SourceRepositoryUrl = DefaultRepositoryUrl });

        var middleware = new SecurityHeadersMiddleware(options);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(mockContext.Object, next);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Sets_X_Source_Code_from_configured_repository_url()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext(
            sourceRepositoryUrl: "https://example.com/fork/lfm");
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await middleware.Invoke(mockContext.Object, next);

        Assert.Equal("https://example.com/fork/lfm", httpContext.Response.Headers["X-Source-Code"].ToString());
    }

    [Fact]
    public async Task Sets_X_Source_Commit_header()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await middleware.Invoke(mockContext.Object, next);

        // Local builds without /p:GitCommit=... return "unknown"; CI supplies
        // the real SHA. Either value is a valid AGPL §13 disclosure when
        // paired with X-Source-Code.
        var commit = httpContext.Response.Headers["X-Source-Commit"].ToString();
        Assert.False(string.IsNullOrEmpty(commit));
    }

    [Fact]
    public async Task Default_Cache_Control_is_private_no_store_when_handler_does_not_set_one()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await middleware.Invoke(mockContext.Object, next);

        Assert.Equal("private, no-store", httpContext.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task Default_Cache_Control_is_present_when_next_throws()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ => throw new InvalidOperationException("downstream failure");

        var act = () => middleware.Invoke(mockContext.Object, next);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Equal("private, no-store", httpContext.Response.Headers["Cache-Control"].ToString());
    }

    [Fact]
    public async Task Does_not_override_Cache_Control_set_by_handler()
    {
        var (middleware, mockContext, httpContext) = CreateTestContext();
        FunctionExecutionDelegate next = _ =>
        {
            httpContext.Response.Headers["Cache-Control"] = "private, max-age=300";
            return Task.CompletedTask;
        };

        await middleware.Invoke(mockContext.Object, next);

        Assert.Equal("private, max-age=300", httpContext.Response.Headers["Cache-Control"].ToString());
    }
}
