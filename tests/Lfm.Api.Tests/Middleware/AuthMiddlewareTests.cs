// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace Lfm.Api.Tests.Middleware;

public class AuthMiddlewareTests
{
    // The ASP.NET Core integration stores HttpContext in Items under this key.
    private const string HttpContextKey = "HttpRequestContext";

    private static AuthOptions DefaultAuthOptions(string cookieName = "battlenet_token") =>
        new() { CookieName = cookieName };

    private static (Mock<FunctionContext> Context, DefaultHttpContext HttpContext, IDictionary<object, object> Items)
        CreateContext(IDictionary<string, string>? cookies = null)
    {
        var httpContext = new DefaultHttpContext();
        if (cookies is not null)
        {
            httpContext.Request.Headers["Cookie"] = string.Join("; ",
                cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        var items = (IDictionary<object, object>)new Dictionary<object, object> { [HttpContextKey] = httpContext };

        var funcContext = new Mock<FunctionContext>();
        funcContext.Setup(c => c.Items).Returns(items);

        return (funcContext, httpContext, items);
    }

    private static SessionPrincipal MakePrincipal(DateTimeOffset expiresAt) =>
        new(
            BattleNetId: "player#1234",
            BattleTag: "Player#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: expiresAt);

    [Fact]
    public async Task Invoke_does_not_set_principal_when_no_cookie_present()
    {
        var cipher = new Mock<ISessionCipher>(MockBehavior.Strict);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext();
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.False(items.ContainsKey(SessionKeys.Principal));
        cipher.Verify(c => c.Unprotect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_does_not_set_principal_when_cookie_value_is_empty()
    {
        var cipher = new Mock<ISessionCipher>(MockBehavior.Strict);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "" });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.False(items.ContainsKey(SessionKeys.Principal));
        cipher.Verify(c => c.Unprotect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Invoke_does_not_set_principal_when_cipher_returns_null()
    {
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns((SessionPrincipal?)null);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "ciphertext" });
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.False(items.ContainsKey(SessionKeys.Principal));
    }

    [Fact]
    public async Task Invoke_does_not_set_principal_when_session_is_expired()
    {
        var expired = MakePrincipal(DateTimeOffset.UtcNow.AddMinutes(-1));
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns(expired);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "ciphertext" });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.False(items.ContainsKey(SessionKeys.Principal));
    }

    // Round-trip identity: the SessionPrincipal returned by ISessionCipher.Unprotect must land in
    // HttpContext.Items unchanged — this pins the middleware's forwarding contract, not cipher behavior.
    [Fact]
    public async Task Invoke_sets_principal_when_cookie_decrypts_to_unexpired_session()
    {
        var principal = MakePrincipal(DateTimeOffset.UtcNow.AddHours(1));
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns(principal);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "ciphertext" });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.Equal(principal, items[SessionKeys.Principal]);
    }

    [Fact]
    public async Task Invoke_uses_configured_cookie_name_not_default()
    {
        // Verify the middleware reads from AuthOptions.CookieName, not a hardcoded string.
        var principal = MakePrincipal(DateTimeOffset.UtcNow.AddHours(1));
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns(principal);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions(cookieName: "custom_cookie")));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string>
        {
            ["battlenet_token"] = "wrong-cookie",
            ["custom_cookie"] = "ciphertext",
        });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.Equal(principal, items[SessionKeys.Principal]);
    }

    [Fact]
    public async Task Invoke_always_calls_next_regardless_of_session_state()
    {
        // Whether or not the session resolves, the pipeline must continue. AuthMiddleware
        // is non-blocking; AuthPolicyMiddleware is the gatekeeper.
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect(It.IsAny<string>())).Returns((SessionPrincipal?)null);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, _) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "junk" });
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_does_not_throw_when_http_context_is_missing()
    {
        var cipher = new Mock<ISessionCipher>(MockBehavior.Strict);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var items = (IDictionary<object, object>)new Dictionary<object, object>();
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        var nextCalled = false;

        await sut.Invoke(ctx.Object, _ => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled);
        Assert.False(items.ContainsKey(SessionKeys.Principal));
    }

    // ── Clock-skew tolerance ──────────────────────────────────────────────
    //
    // A small grace window guards against cross-instance clock drift between
    // the Functions instance that issued the cookie and the one validating it.
    // The boundary is pinned at ExpiresAt + ClockSkew.

    [Fact]
    public void ClockSkew_is_thirty_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), AuthMiddleware.ClockSkew);
    }

    [Fact]
    public async Task Invoke_sets_principal_when_session_expired_within_clock_skew()
    {
        // Principal whose declared expiry is 5 s in the past: inside the 30 s
        // skew window, so still acceptable.
        var principal = MakePrincipal(DateTimeOffset.UtcNow.AddSeconds(-5));
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns(principal);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "ciphertext" });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.Equal(principal, items[SessionKeys.Principal]);
    }

    [Fact]
    public async Task Invoke_does_not_set_principal_when_session_expired_beyond_clock_skew()
    {
        // Principal whose declared expiry is 60 s in the past: well beyond the
        // 30 s skew window, so rejected.
        var principal = MakePrincipal(DateTimeOffset.UtcNow.AddSeconds(-60));
        var cipher = new Mock<ISessionCipher>();
        cipher.Setup(c => c.Unprotect("ciphertext")).Returns(principal);
        var sut = new AuthMiddleware(cipher.Object, MsOptions.Create(DefaultAuthOptions()));
        var (ctx, _, items) = CreateContext(new Dictionary<string, string> { ["battlenet_token"] = "ciphertext" });

        await sut.Invoke(ctx.Object, _ => Task.CompletedTask);

        Assert.False(items.ContainsKey(SessionKeys.Principal));
    }
}
