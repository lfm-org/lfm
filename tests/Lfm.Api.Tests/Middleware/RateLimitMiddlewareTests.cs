// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class RateLimitMiddlewareTests
{
    // The ASP.NET Core integration stores HttpContext in Items under this key.
    // See: Azure/azure-functions-dotnet-worker Constants.HttpContextKey
    private const string HttpContextKey = "HttpRequestContext";

    private static RateLimitOptions DefaultOptions(bool enabled = true) => new()
    {
        AuthRequestsPerMinute = 10,
        WriteRequestsPerMinute = 30,
        Enabled = enabled,
    };

    private static (Mock<FunctionContext> Context, DefaultHttpContext HttpContext) CreateContext(
        string functionName,
        string method,
        string? clientIp = "192.168.1.1",
        string? xForwardedFor = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        if (clientIp is not null)
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);
        }

        if (xForwardedFor is not null)
        {
            httpContext.Request.Headers["X-Forwarded-For"] = xForwardedFor;
        }

        var funcDef = new Mock<FunctionDefinition>();
        funcDef.Setup(f => f.Name).Returns(functionName);

        // GetHttpContext() reads context.Items[HttpContextKey]
        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var funcContext = new Mock<FunctionContext>();
        funcContext.Setup(c => c.FunctionDefinition).Returns(funcDef.Object);
        funcContext.Setup(c => c.Items).Returns(items);

        return (funcContext, httpContext);
    }

    [Fact]
    public async Task Request_under_limit_passes_through()
    {
        var opts = MsOptions.Create(DefaultOptions());
        var middleware = new RateLimitMiddleware(opts);
        var (ctx, httpCtx) = CreateContext("runs-create", "POST");
        var nextCalled = false;

        await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(429, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Request_at_limit_returns_429_with_retry_after()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 2;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Exhaust the limit
        for (var i = 0; i < 2; i++)
        {
            var (ctx, _) = CreateContext("runs-create", "POST");
            await middleware.Invoke(ctx.Object, _ => Task.CompletedTask);
        }

        // Next request should be blocked
        var (blockedCtx, blockedHttpCtx) = CreateContext("runs-create", "POST");
        blockedHttpCtx.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(blockedCtx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, blockedHttpCtx.Response.StatusCode);
        Assert.Equal("60", blockedHttpCtx.Response.Headers["Retry-After"].ToString());
    }

    [Fact]
    public async Task Different_ips_get_separate_buckets()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // IP 1 uses its permit
        var (ctx1, _) = CreateContext("runs-create", "POST", clientIp: "10.0.0.1");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // IP 2 should still have its own permit
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST", clientIp: "10.0.0.2");
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Auth_endpoint_uses_auth_bucket_not_write_bucket()
    {
        var options = DefaultOptions();
        options.AuthRequestsPerMinute = 1;
        options.WriteRequestsPerMinute = 100;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Exhaust auth limit with a POST to battlenet-callback
        var (authCtx1, _) = CreateContext("battlenet-callback", "POST");
        await middleware.Invoke(authCtx1.Object, _ => Task.CompletedTask);

        // Auth limit exhausted — next auth POST should be blocked
        var (authCtx2, authHttp2) = CreateContext("battlenet-callback", "POST");
        authHttp2.Response.Body = new MemoryStream();
        var authNextCalled = false;

        await middleware.Invoke(authCtx2.Object, _ => { authNextCalled = true; return Task.CompletedTask; });

        Assert.False(authNextCalled);
        Assert.Equal(429, authHttp2.Response.StatusCode);

        // Write endpoint should still work (separate bucket with 100 limit)
        var (writeCtx, writeHttp) = CreateContext("runs-create", "POST");
        var writeNextCalled = false;

        await middleware.Invoke(writeCtx.Object, _ => { writeNextCalled = true; return Task.CompletedTask; });

        Assert.True(writeNextCalled);
        Assert.NotEqual(429, writeHttp.Response.StatusCode);
    }

    [Fact]
    public async Task Write_endpoint_uses_write_bucket()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Exhaust write limit with a POST
        var (ctx1, _) = CreateContext("runs-create", "POST");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Next write (PUT) should be blocked — same IP, same write bucket
        var (ctx2, httpCtx2) = CreateContext("runs-update", "PUT");
        httpCtx2.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Get_request_is_not_rate_limited()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        options.AuthRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Make many GET requests — none should be blocked
        for (var i = 0; i < 50; i++)
        {
            var (ctx, httpCtx) = CreateContext("runs-list", "GET");
            var nextCalled = false;

            await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

            Assert.True(nextCalled);
            Assert.NotEqual(429, httpCtx.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Forwarded_for_header_overrides_remote_ip_for_bucketing()
    {
        // When the X-Forwarded-For header is present, the leftmost address must be
        // used as the bucket key — otherwise a proxy in front of the function would
        // collapse all client traffic into one bucket.
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // First request: forwarded client A behind proxy 10.0.0.1
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.1, 10.0.0.1");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Second request: same proxy, different forwarded client B → must get its own bucket
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.2, 10.0.0.1");
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Forwarded_for_with_single_address_uses_it_as_bucket_key()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Burn the limit for forwarded client A
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.10");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Second request from same forwarded client must be 429 — proves the
        // bucket key was the forwarded IP, not the proxy IP.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.10");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Forwarded_for_trims_whitespace_around_addresses()
    {
        // X-Forwarded-For commonly has spaces after commas — the parser must
        // trim them so the leftmost address is matched correctly.
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.50,   10.0.0.1");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            // Same client, no whitespace this time — must hit the same bucket
            xForwardedFor: "203.0.113.50,10.0.0.1");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Empty_forwarded_for_falls_back_to_remote_ip()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.5",
            xForwardedFor: "");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.5",
            xForwardedFor: null);
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Disabled_rate_limiting_passes_all_requests()
    {
        var options = DefaultOptions(enabled: false);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Even after exceeding the notional limit, requests pass through
        for (var i = 0; i < 5; i++)
        {
            var (ctx, httpCtx) = CreateContext("runs-create", "POST");
            var nextCalled = false;

            await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

            Assert.True(nextCalled);
            Assert.NotEqual(429, httpCtx.Response.StatusCode);
        }
    }
}
