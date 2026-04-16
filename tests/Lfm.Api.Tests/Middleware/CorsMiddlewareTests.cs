// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class CorsMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";
    private const string AllowedOrigin = "https://app.lfm.example";
    private const string DisallowedOrigin = "https://evil.example";

    private static CorsOptions DefaultOptions() => new()
    {
        AllowedOrigins = new[] { AllowedOrigin, "https://other.lfm.example" },
    };

    private static (Mock<FunctionContext> Context, DefaultHttpContext HttpContext) CreateContext(
        string method,
        string? origin = null,
        string? requestedMethod = null,
        string? requestedHeaders = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        if (origin is not null)
            httpContext.Request.Headers.Origin = origin;
        if (requestedMethod is not null)
            httpContext.Request.Headers["Access-Control-Request-Method"] = requestedMethod;
        if (requestedHeaders is not null)
            httpContext.Request.Headers["Access-Control-Request-Headers"] = requestedHeaders;

        var items = (IDictionary<object, object>)new Dictionary<object, object> { [HttpContextKey] = httpContext };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return (ctx, httpContext);
    }

    [Fact]
    public async Task Allowed_origin_get_request_gets_cors_headers_and_passes_through()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: AllowedOrigin);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.Equal(AllowedOrigin, httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString());
        Assert.Equal("true", httpCtx.Response.Headers["Access-Control-Allow-Credentials"].ToString());
        Assert.Equal("Origin", httpCtx.Response.Headers.Vary.ToString());
    }

    [Fact]
    public async Task Disallowed_origin_does_not_get_cors_headers_but_passes_through()
    {
        // Defensive: a non-preflight request from an evil origin still flows to the
        // function (the function may itself reject), but no Allow-Origin header is set
        // so the browser drops the response.
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: DisallowedOrigin);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.False(httpCtx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
        Assert.False(httpCtx.Response.Headers.ContainsKey("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Origin_match_is_case_insensitive()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: AllowedOrigin.ToUpperInvariant());

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.Equal(
            AllowedOrigin.ToUpperInvariant(),
            httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString());
    }

    [Fact]
    public async Task Missing_origin_passes_through_without_cors_headers()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.False(httpCtx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Preflight_options_request_short_circuits_with_204()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext(
            "OPTIONS",
            origin: AllowedOrigin,
            requestedMethod: "POST",
            requestedHeaders: "Content-Type, Authorization");
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, httpCtx.Response.StatusCode);
        Assert.Equal("POST", httpCtx.Response.Headers["Access-Control-Allow-Methods"].ToString());
        Assert.Equal("Content-Type, Authorization", httpCtx.Response.Headers["Access-Control-Allow-Headers"].ToString());
        Assert.Equal("3600", httpCtx.Response.Headers["Access-Control-Max-Age"].ToString());
        Assert.Equal(AllowedOrigin, httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString());
    }

    [Fact]
    public async Task Preflight_does_not_emit_request_headers_response_when_none_were_requested()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext(
            "OPTIONS",
            origin: AllowedOrigin,
            requestedMethod: "POST",
            requestedHeaders: null);

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.False(httpCtx.Response.Headers.ContainsKey("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task Preflight_from_disallowed_origin_still_short_circuits_but_omits_origin_header()
    {
        // The middleware short-circuits all OPTIONS requests, even from disallowed origins.
        // Without the Allow-Origin header the browser will reject the response client-side.
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext(
            "OPTIONS",
            origin: DisallowedOrigin,
            requestedMethod: "POST");
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, httpCtx.Response.StatusCode);
        Assert.False(httpCtx.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task No_http_context_passes_through_without_throwing()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns((IDictionary<object, object>)new Dictionary<object, object>());
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
    }
}
