using FluentAssertions;
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

        nextCalled.Should().BeTrue();
        httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be(AllowedOrigin);
        httpCtx.Response.Headers["Access-Control-Allow-Credentials"].ToString().Should().Be("true");
        httpCtx.Response.Headers.Vary.ToString().Should().Be("Origin");
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

        nextCalled.Should().BeTrue();
        httpCtx.Response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
        httpCtx.Response.Headers.Should().NotContainKey("Access-Control-Allow-Credentials");
    }

    [Fact]
    public async Task Origin_match_is_case_insensitive()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: AllowedOrigin.ToUpperInvariant());

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString()
            .Should().Be(AllowedOrigin.ToUpperInvariant(),
                "the response echoes the request's Origin header verbatim once it matches the allow list");
    }

    [Fact]
    public async Task Missing_origin_passes_through_without_cors_headers()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var (ctx, httpCtx) = CreateContext("GET", origin: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
        httpCtx.Response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
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

        nextCalled.Should().BeFalse("preflight must short-circuit and never invoke the function");
        httpCtx.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        httpCtx.Response.Headers["Access-Control-Allow-Methods"].ToString().Should().Be("POST");
        httpCtx.Response.Headers["Access-Control-Allow-Headers"].ToString().Should().Be("Content-Type, Authorization");
        httpCtx.Response.Headers["Access-Control-Max-Age"].ToString().Should().Be("3600");
        httpCtx.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be(AllowedOrigin);
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

        httpCtx.Response.Headers.Should().NotContainKey("Access-Control-Allow-Headers");
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

        nextCalled.Should().BeFalse();
        httpCtx.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        httpCtx.Response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task No_http_context_passes_through_without_throwing()
    {
        var sut = new CorsMiddleware(MsOptions.Create(DefaultOptions()));
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns((IDictionary<object, object>)new Dictionary<object, object>());
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        nextCalled.Should().BeTrue();
    }
}
