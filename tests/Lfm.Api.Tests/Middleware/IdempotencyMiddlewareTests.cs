// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests.Middleware;

public class IdempotencyMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";

    private static (IdempotencyMiddleware middleware, Mock<FunctionContext> context, DefaultHttpContext httpContext, Mock<IIdempotencyStore> store) Setup(
        string method = "POST",
        string? idempotencyKey = "abc-123",
        SessionPrincipal? principal = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Response.Body = new MemoryStream();
        if (idempotencyKey is not null)
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;

        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };
        if (principal is not null)
            items[SessionKeys.Principal] = principal;

        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);

        var store = new Mock<IIdempotencyStore>();
        var options = MSOptions.Create(new IdempotencyOptions { ContainerName = "idempotency", TtlSeconds = 86400 });
        var middleware = new IdempotencyMiddleware(store.Object, options, NullLogger<IdempotencyMiddleware>.Instance);

        return (middleware, ctx, httpContext, store);
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1") =>
        new(
            BattleNetId: battleNetId,
            BattleTag: "P#1",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Passes_through_when_request_method_is_GET()
    {
        var (middleware, ctx, _, store) = Setup(method: "GET", principal: MakePrincipal());
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.True(nextCalled);
        store.Verify(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Passes_through_when_idempotency_key_absent()
    {
        var (middleware, ctx, _, store) = Setup(idempotencyKey: null, principal: MakePrincipal());
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.True(nextCalled);
        store.Verify(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Passes_through_when_principal_missing()
    {
        // Anonymous callers get no idempotency — no partition key to bind to.
        var (middleware, ctx, _, store) = Setup(principal: null);
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.True(nextCalled);
        store.Verify(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rejects_empty_key_with_400()
    {
        var (middleware, ctx, httpContext, _) = Setup(idempotencyKey: "   ", principal: MakePrincipal());
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);
    }

    [Fact]
    public async Task Rejects_oversized_key_with_400()
    {
        var longKey = new string('k', 256);
        var (middleware, ctx, httpContext, _) = Setup(idempotencyKey: longKey, principal: MakePrincipal());
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Replays_cached_status_on_hit_without_invoking_next()
    {
        var (middleware, ctx, httpContext, store) = Setup(principal: MakePrincipal("bnet-1"));
        store.Setup(s => s.TryGetAsync("bnet-1", "abc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyEntry(
                Id: "bnet-1:abc-123",
                BattleNetId: "bnet-1",
                IdempotencyKey: "abc-123",
                StatusCode: 201,
                ETag: "\"orig\"",
                BodyHash: null,
                CreatedAt: "2026-04-24T00:00:00Z",
                Ttl: 86400));

        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await middleware.Invoke(ctx.Object, next);

        Assert.False(nextCalled);
        Assert.Equal(201, httpContext.Response.StatusCode);
        Assert.Equal("true", httpContext.Response.Headers["Idempotent-Replay"].ToString());
        Assert.Equal("\"orig\"", httpContext.Response.Headers.ETag.ToString());

        httpContext.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(httpContext.Response.Body);
        Assert.Equal(
            "https://github.com/lfm-org/lfm/errors#idempotent-replay",
            doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Stores_entry_on_success_and_passes_response_through()
    {
        var (middleware, ctx, httpContext, store) = Setup(principal: MakePrincipal("bnet-1"));
        store.Setup(s => s.TryGetAsync("bnet-1", "abc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyEntry?)null);

        FunctionExecutionDelegate next = _ =>
        {
            httpContext.Response.StatusCode = 201;
            httpContext.Response.Headers.ETag = "\"new\"";
            return Task.CompletedTask;
        };

        await middleware.Invoke(ctx.Object, next);

        Assert.Equal(201, httpContext.Response.StatusCode);
        store.Verify(s => s.PutAsync(
            It.Is<IdempotencyEntry>(e =>
                e.BattleNetId == "bnet-1"
                && e.IdempotencyKey == "abc-123"
                && e.StatusCode == 201
                && e.ETag == "\"new\""),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Does_not_store_on_4xx()
    {
        var (middleware, ctx, httpContext, store) = Setup(principal: MakePrincipal("bnet-1"));
        store.Setup(s => s.TryGetAsync("bnet-1", "abc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyEntry?)null);

        FunctionExecutionDelegate next = _ =>
        {
            httpContext.Response.StatusCode = 400;
            return Task.CompletedTask;
        };

        await middleware.Invoke(ctx.Object, next);

        // Non-2xx responses must not be cached — a retry should re-evaluate.
        store.Verify(s => s.PutAsync(It.IsAny<IdempotencyEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Store_failure_does_not_break_response()
    {
        // Best-effort persistence: if Cosmos blinks after the handler has
        // already succeeded, the client must still get their 2xx.
        var (middleware, ctx, httpContext, store) = Setup(principal: MakePrincipal("bnet-1"));
        store.Setup(s => s.TryGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyEntry?)null);
        store.Setup(s => s.PutAsync(It.IsAny<IdempotencyEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cosmos down"));

        FunctionExecutionDelegate next = _ =>
        {
            httpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        await middleware.Invoke(ctx.Object, next);

        Assert.Equal(200, httpContext.Response.StatusCode);
    }
}
