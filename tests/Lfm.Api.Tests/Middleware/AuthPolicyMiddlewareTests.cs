// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class AuthPolicyMiddlewareTests
{
    private const string HttpContextKey = "HttpRequestContext";

    // Real API function entry points used to exercise the reflection-based RequiresAuth lookup.
    // RunsUpdateFunction.Run carries [RequireAuth]; HealthFunction.Live does not.
    private const string AuthorizedEndpoint = "Lfm.Api.Functions.RunsUpdateFunction.Run";
    private const string AnonymousEndpoint = "Lfm.Api.Functions.HealthFunction.Live";

    private static (Mock<FunctionContext> Context, DefaultHttpContext HttpContext)
        CreateContext(string entryPoint, SessionPrincipal? principal = null)
    {
        var httpContext = new DefaultHttpContext();
        var items = new Dictionary<object, object> { [HttpContextKey] = httpContext };
        if (principal is not null)
            items[SessionKeys.Principal] = principal;

        var funcDef = new Mock<FunctionDefinition>();
        funcDef.Setup(f => f.EntryPoint).Returns(entryPoint);
        funcDef.Setup(f => f.Name).Returns(entryPoint);

        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.FunctionDefinition).Returns(funcDef.Object);
        ctx.Setup(c => c.Items).Returns((IDictionary<object, object>)items);

        return (ctx, httpContext);
    }

    private static SessionPrincipal MakePrincipal() =>
        new(
            BattleNetId: "player#1234",
            BattleTag: "Player#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Anonymous_function_passes_through_without_principal()
    {
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext(AnonymousEndpoint, principal: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Authorized_function_returns_401_without_principal()
    {
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext(AuthorizedEndpoint, principal: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.False(nextCalled);
        Assert.Equal(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Authorized_function_passes_through_when_principal_is_present()
    {
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext(AuthorizedEndpoint, principal: MakePrincipal());
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_function_passes_through_even_when_principal_is_present()
    {
        // Auth-required is a strict gate; absence of [RequireAuth] does not flip when a principal is present.
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext(AnonymousEndpoint, principal: MakePrincipal());
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Reflection_cache_returns_consistent_decision_across_calls()
    {
        // Two consecutive invocations of the same authorized endpoint should both
        // return 401 without principal — the per-EntryPoint cache must not flip after the first hit.
        var sut = new AuthPolicyMiddleware();
        var (ctx1, http1) = CreateContext(AuthorizedEndpoint, principal: null);
        var (ctx2, http2) = CreateContext(AuthorizedEndpoint, principal: null);

        await sut.Invoke(ctx1.Object, _ => Task.CompletedTask);
        await sut.Invoke(ctx2.Object, _ => Task.CompletedTask);

        Assert.Equal(401, http1.Response.StatusCode);
        Assert.Equal(401, http2.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_type_in_entry_point_defaults_to_anonymous()
    {
        // Defensive default: if the EntryPoint can't be resolved, the middleware
        // does not require auth. This avoids accidentally locking down test/dev contexts.
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext("Lfm.Api.NotARealType.NotAMethod", principal: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Entry_point_without_dot_defaults_to_anonymous()
    {
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext("nodotsentrypoint", principal: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_method_on_existing_type_defaults_to_anonymous()
    {
        var sut = new AuthPolicyMiddleware();
        var (ctx, httpCtx) = CreateContext("Lfm.Api.Functions.HealthFunction.NoSuchMethod", principal: null);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.NotEqual(401, httpCtx.Response.StatusCode);
    }
}
