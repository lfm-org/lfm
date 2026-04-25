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

    private static RateLimitOptions DefaultOptions(
        bool enabled = true,
        IReadOnlyList<string>? trustedProxies = null) => new()
        {
            AuthRequestsPerMinute = 10,
            WriteRequestsPerMinute = 30,
            ReadRequestsPerMinute = 120,
            Enabled = enabled,
            TrustedProxyAddresses = trustedProxies ?? [],
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

    // ── Write-bucket behaviour ────────────────────────────────────────────

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

        for (var i = 0; i < 2; i++)
        {
            var (ctx, _) = CreateContext("runs-create", "POST");
            await middleware.Invoke(ctx.Object, _ => Task.CompletedTask);
        }

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

        var (ctx1, _) = CreateContext("runs-create", "POST", clientIp: "10.0.0.1");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

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

        var (authCtx1, _) = CreateContext("battlenet-callback", "POST");
        await middleware.Invoke(authCtx1.Object, _ => Task.CompletedTask);

        var (authCtx2, authHttp2) = CreateContext("battlenet-callback", "POST");
        authHttp2.Response.Body = new MemoryStream();
        var authNextCalled = false;

        await middleware.Invoke(authCtx2.Object, _ => { authNextCalled = true; return Task.CompletedTask; });

        Assert.False(authNextCalled);
        Assert.Equal(429, authHttp2.Response.StatusCode);

        var (writeCtx, writeHttp) = CreateContext("runs-create", "POST");
        var writeNextCalled = false;

        await middleware.Invoke(writeCtx.Object, _ => { writeNextCalled = true; return Task.CompletedTask; });

        Assert.True(writeNextCalled);
        Assert.NotEqual(429, writeHttp.Response.StatusCode);
    }

    [Theory]
    [InlineData("battlenet-callback-v1")]
    [InlineData("battlenet-login-v1")]
    public async Task V1_auth_alias_uses_auth_bucket(string functionName)
    {
        // Regression for the unrate-limited v1 alias gap. The /api/v1/ aliases
        // call the same handlers as the unversioned routes, so the strict
        // auth-tier limit must apply to them too. If the alias falls through to
        // the read tier, this test fails.
        var options = DefaultOptions();
        options.AuthRequestsPerMinute = 1;
        options.ReadRequestsPerMinute = 100;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext(functionName, "GET");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext(functionName, "GET");
        httpCtx2.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task V1_privacy_alias_uses_privacy_bucket()
    {
        // Same regression for /api/v1/privacy-contact/email — must use the
        // strict privacy tier, not fall through to ReadRequestsPerMinute.
        var options = DefaultOptions();
        options.PrivacyRequestsPerMinute = 1;
        options.ReadRequestsPerMinute = 100;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("privacy-email-v1", "GET");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext("privacy-email-v1", "GET");
        httpCtx2.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Write_endpoint_uses_write_bucket()
    {
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext("runs-update", "PUT");
        httpCtx2.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    // ── Read bucket (NEW in W3) ───────────────────────────────────────────

    [Fact]
    public async Task Get_request_under_read_limit_passes_through()
    {
        var options = DefaultOptions();
        options.ReadRequestsPerMinute = 3;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        for (var i = 0; i < 3; i++)
        {
            var (ctx, httpCtx) = CreateContext("runs-list", "GET");
            var nextCalled = false;

            await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

            Assert.True(nextCalled);
            Assert.NotEqual(429, httpCtx.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Get_request_at_read_limit_returns_429()
    {
        var options = DefaultOptions();
        options.ReadRequestsPerMinute = 2;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        for (var i = 0; i < 2; i++)
        {
            var (ctx, _) = CreateContext("runs-list", "GET");
            await middleware.Invoke(ctx.Object, _ => Task.CompletedTask);
        }

        var (blockedCtx, blockedHttpCtx) = CreateContext("runs-list", "GET");
        blockedHttpCtx.Response.Body = new MemoryStream();
        var nextCalled = false;

        await middleware.Invoke(blockedCtx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(429, blockedHttpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Read_bucket_is_separate_from_write_bucket()
    {
        // Exhausting the read budget on /runs (GET) must not block a POST to /runs.
        var options = DefaultOptions();
        options.ReadRequestsPerMinute = 1;
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (readCtx, _) = CreateContext("runs-list", "GET");
        await middleware.Invoke(readCtx.Object, _ => Task.CompletedTask);

        var (readCtx2, readHttp2) = CreateContext("runs-list", "GET");
        readHttp2.Response.Body = new MemoryStream();
        await middleware.Invoke(readCtx2.Object, _ => Task.CompletedTask);
        Assert.Equal(429, readHttp2.Response.StatusCode);

        // Write bucket must still have a fresh permit.
        var (writeCtx, writeHttp) = CreateContext("runs-create", "POST");
        var writeNextCalled = false;

        await middleware.Invoke(writeCtx.Object, _ => { writeNextCalled = true; return Task.CompletedTask; });

        Assert.True(writeNextCalled);
        Assert.NotEqual(429, writeHttp.Response.StatusCode);
    }

    // ── X-Forwarded-For trust (W2) ────────────────────────────────────────
    //
    // The contract after W2: X-Forwarded-For is only honoured when the TCP
    // peer (`Connection.RemoteIpAddress`) appears in
    // `RateLimitOptions.TrustedProxyAddresses`. When honoured, the RIGHTMOST
    // entry is used as the bucket key — the only hop the trusted proxy
    // actually witnessed. When not honoured, XFF is ignored entirely and the
    // bucket key is the TCP peer.

    [Fact]
    public async Task Xff_from_untrusted_remote_is_ignored_for_bucketing()
    {
        // A direct attacker setting X-Forwarded-For to forge a fresh bucket
        // must fall into their real-IP bucket. Without this, they can bypass
        // rate limits indefinitely by rotating XFF values.
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        // No trusted proxies configured — every request is treated as direct.
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Attacker at 198.51.100.1 forges XFF pointing at a different IP.
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "198.51.100.1",
            xForwardedFor: "203.0.113.1");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Same attacker, different forged XFF — must hit the same bucket
        // (keyed on 198.51.100.1) and be blocked.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "198.51.100.1",
            xForwardedFor: "203.0.113.2");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Xff_from_trusted_remote_is_used_rightmost_for_bucketing()
    {
        // The request arrives from a configured proxy; the rightmost XFF
        // entry identifies the last hop the proxy witnessed.
        var options = DefaultOptions(trustedProxies: ["10.0.0.1"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Proxy forwards a single hop. Bucket key = rightmost = 203.0.113.50.
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.50");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Same client via the trusted proxy → same bucket → blocked.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.50");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Xff_from_trusted_remote_takes_rightmost_not_leftmost()
    {
        // When the XFF chain has multiple entries, the rightmost is the one
        // the trusted proxy actually recorded. Any earlier entries could
        // have been forged by the original client.
        var options = DefaultOptions(trustedProxies: ["10.0.0.1"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // First request: client A (spoofed leftmost) + witnessed hop 203.0.113.99
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "198.51.100.77, 203.0.113.99");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Second request: different forged leftmost, same witnessed rightmost.
        // Bucket must be shared — keyed on the rightmost entry.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "198.51.100.88, 203.0.113.99");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Xff_different_rightmost_entries_from_trusted_proxy_get_separate_buckets()
    {
        // Covers the mutant where the proxy IP is used as the bucket key
        // (ignoring XFF) or where a constant is used (collapsing all clients
        // behind one proxy). Two requests with *different* rightmost entries
        // through the same trusted proxy must land in separate buckets.
        var options = DefaultOptions(trustedProxies: ["10.0.0.1"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        // Client A exhausts their bucket.
        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.10");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Client B via the same trusted proxy must have its own permit.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "203.0.113.20");
        var nextCalled = false;

        await middleware.Invoke(ctx2.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Xff_trims_whitespace_around_addresses()
    {
        var options = DefaultOptions(trustedProxies: ["10.0.0.1"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "198.51.100.1,   203.0.113.50");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Same rightmost entry without extra whitespace must hit the same bucket.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.1",
            xForwardedFor: "198.51.100.1,203.0.113.50");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Empty_xff_from_trusted_remote_falls_back_to_remote_ip()
    {
        // Empty-string XFF header must resolve to the remote-IP bucket.
        var options = DefaultOptions(trustedProxies: ["10.0.0.5"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.5",
            xForwardedFor: "");
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        // Second request with a distinct but still-empty XFF input proves the
        // bucket is keyed on the TCP peer, not on the XFF literal.
        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.5",
            xForwardedFor: "");
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    [Fact]
    public async Task Missing_xff_from_trusted_remote_falls_back_to_remote_ip()
    {
        // Separate from the empty-string case: an absent XFF header is a
        // different input path (no Request.Headers entry at all), so it gets
        // its own test to isolate failure modes.
        var options = DefaultOptions(trustedProxies: ["10.0.0.6"]);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        var (ctx1, _) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.6",
            xForwardedFor: null);
        await middleware.Invoke(ctx1.Object, _ => Task.CompletedTask);

        var (ctx2, httpCtx2) = CreateContext("runs-create", "POST",
            clientIp: "10.0.0.6",
            xForwardedFor: null);
        httpCtx2.Response.Body = new MemoryStream();

        await middleware.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(429, httpCtx2.Response.StatusCode);
    }

    // ── Misc ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disabled_rate_limiting_passes_all_requests()
    {
        var options = DefaultOptions(enabled: false);
        options.WriteRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        for (var i = 0; i < 5; i++)
        {
            var (ctx, httpCtx) = CreateContext("runs-create", "POST");
            var nextCalled = false;

            await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

            Assert.True(nextCalled);
            Assert.NotEqual(429, httpCtx.Response.StatusCode);
        }
    }

    [Fact]
    public async Task Options_request_bypasses_rate_limit_entirely()
    {
        // CorsMiddleware handles preflight; RateLimit must not count OPTIONS.
        var options = DefaultOptions();
        options.WriteRequestsPerMinute = 1;
        options.ReadRequestsPerMinute = 1;
        options.AuthRequestsPerMinute = 1;
        var opts = MsOptions.Create(options);
        var middleware = new RateLimitMiddleware(opts);

        for (var i = 0; i < 20; i++)
        {
            var (ctx, httpCtx) = CreateContext("cors-preflight", "OPTIONS");
            var nextCalled = false;

            await middleware.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

            Assert.True(nextCalled);
            Assert.NotEqual(429, httpCtx.Response.StatusCode);
        }
    }
}
