// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Moq;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests.Middleware;

public class RequestSizeLimitMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";

    private static (RequestSizeLimitMiddleware middleware, Mock<FunctionContext> context, DefaultHttpContext httpContext) CreateTestContext(
        int maxBytes = 1024,
        long? contentLength = null,
        byte[]? body = null)
    {
        var httpContext = new DefaultHttpContext();
        if (contentLength is long cl) httpContext.Request.ContentLength = cl;
        if (body is not null) httpContext.Request.Body = new MemoryStream(body);
        httpContext.Response.Body = new MemoryStream();

        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };
        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(items);

        var options = MSOptions.Create(new RequestSizeLimitOptions { MaxBytes = maxBytes });
        return (new RequestSizeLimitMiddleware(options), mockContext, httpContext);
    }

    [Fact]
    public async Task Passes_through_when_content_length_within_cap()
    {
        var (middleware, ctx, httpContext) = CreateTestContext(maxBytes: 1024, contentLength: 500);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status413PayloadTooLarge, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Passes_through_when_content_length_absent()
    {
        // GET / DELETE have no body and no Content-Length. The middleware must
        // not reject these — that would break every read on the API.
        var (middleware, ctx, httpContext) = CreateTestContext(maxBytes: 1024);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Rejects_413_when_content_length_exceeds_cap()
    {
        var (middleware, ctx, httpContext) = CreateTestContext(maxBytes: 1024, contentLength: 1025);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task Emits_problem_json_with_type_uri_rooted_at_errors_slug()
    {
        var (middleware, ctx, httpContext) = CreateTestContext(maxBytes: 10, contentLength: 11);
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await middleware.Invoke(ctx.Object, next);

        httpContext.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(httpContext.Response.Body);
        Assert.Equal(
            "https://github.com/lfm-org/lfm/errors#payload-too-large",
            doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(413, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Passes_through_when_http_context_missing()
    {
        // Non-HTTP invocations (timer triggers) should never be rejected by
        // this middleware. The guard must be a no-op on them.
        var mockContext = new Mock<FunctionContext>();
        mockContext.Setup(c => c.Items).Returns(new Dictionary<object, object>());
        var options = MSOptions.Create(new RequestSizeLimitOptions { MaxBytes = 1024 });
        var middleware = new RequestSizeLimitMiddleware(options);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(mockContext.Object, next);

        Assert.True(nextCalled);
    }
}
