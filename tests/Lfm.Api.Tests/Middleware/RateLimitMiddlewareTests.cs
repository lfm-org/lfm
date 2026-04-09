using System.Net;
using FluentAssertions;
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

        nextCalled.Should().BeTrue();
        httpCtx.Response.StatusCode.Should().NotBe(429);
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

        nextCalled.Should().BeFalse();
        blockedHttpCtx.Response.StatusCode.Should().Be(429);
        blockedHttpCtx.Response.Headers["Retry-After"].ToString().Should().Be("60");
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

        nextCalled.Should().BeTrue();
        httpCtx2.Response.StatusCode.Should().NotBe(429);
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

        authNextCalled.Should().BeFalse();
        authHttp2.Response.StatusCode.Should().Be(429);

        // Write endpoint should still work (separate bucket with 100 limit)
        var (writeCtx, writeHttp) = CreateContext("runs-create", "POST");
        var writeNextCalled = false;

        await middleware.Invoke(writeCtx.Object, _ => { writeNextCalled = true; return Task.CompletedTask; });

        writeNextCalled.Should().BeTrue();
        writeHttp.Response.StatusCode.Should().NotBe(429);
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

        nextCalled.Should().BeFalse();
        httpCtx2.Response.StatusCode.Should().Be(429);
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

            nextCalled.Should().BeTrue();
            httpCtx.Response.StatusCode.Should().NotBe(429);
        }
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

            nextCalled.Should().BeTrue();
            httpCtx.Response.StatusCode.Should().NotBe(429);
        }
    }
}
