using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

using BlizzardOptions = Lfm.Api.Options.BlizzardOptions;
using AuthOptions = Lfm.Api.Options.AuthOptions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BattleNetCallbackFunction"/>.
///
/// Three required cases:
///   1. Happy path — valid code + state → sets auth cookie + redirects to AppBaseUrl.
///   2. Invalid state — missing or mismatched state → redirects to failure URL.
///   3. Failed code exchange — ExchangeCodeAsync throws → redirects to failure URL.
/// </summary>
public class BattleNetCallbackFunctionTests
{
    private const string AppBaseUrl = "https://example.com";
    private const string FailureUrl = "https://example.com/auth/failure";
    private const string FakeCookieName = "battlenet_token";

    private const string FakeState = "test-state-xyz";
    private const string FakeVerifier = "test-verifier-abc";
    private const string FakeCode = "auth-code-123";
    private const string FakeToken = "access-token-456";
    private const string FakeEncrypted = "encrypted-session-payload";

    private static readonly BlizzardUserInfo FakeUser = new(Id: 999L, BattleTag: "TestUser#1234");
    private static readonly BlizzardTokenResponse FakeTokenResponse = new(FakeToken, 86400);

    private static (BattleNetCallbackFunction fn, HttpContext httpContext) MakeFunction(
        Mock<IBlizzardOAuthClient> oauthMock,
        Mock<ISessionCipher> cipherMock,
        Mock<IRaidersRepository> repoMock,
        TestLogger<BattleNetCallbackFunction>? logger = null)
    {
        var blizzardOpts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Region = "eu",
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = AppBaseUrl,
        });
        var authOpts = MsOptions.Create(new AuthOptions
        {
            DataProtectionKeyUri = "https://kv.example.com/keys/dp",
            CookieName = FakeCookieName,
            CookieMaxAgeHours = 24,
        });

        var fn = new BattleNetCallbackFunction(
            oauthMock.Object,
            cipherMock.Object,
            repoMock.Object,
            blizzardOpts,
            authOpts,
            logger ?? new TestLogger<BattleNetCallbackFunction>());

        var httpContext = new DefaultHttpContext();
        return (fn, httpContext);
    }

    /// <summary>
    /// Builds an <see cref="HttpRequest"/> with the given query parameters and cookies.
    /// </summary>
    private static HttpRequest BuildRequest(
        HttpContext httpContext,
        Dictionary<string, string>? query = null,
        Dictionary<string, string>? cookies = null)
    {
        if (query is not null)
        {
            httpContext.Request.QueryString = new QueryString(
                "?" + string.Join("&", query.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")));
        }

        if (cookies is not null)
        {
            // Inject cookies via the request headers — DefaultHttpContext reads cookies
            // from the Cookie request header.
            var cookieHeader = string.Join("; ",
                cookies.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            httpContext.Request.Headers.Cookie = cookieHeader;
        }

        return httpContext.Request;
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_happy_path_sets_auth_cookie_and_redirects_to_app_base_url()
    {
        // Arrange — no redirect stored in login state (falls back to AppBaseUrl)
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns((FakeState, FakeVerifier, (string?)null));
        oauthMock
            .Setup(o => o.ExchangeCodeAsync(FakeCode, FakeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeTokenResponse);
        oauthMock
            .Setup(o => o.GetUserInfoAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeUser);

        repoMock
            .Setup(r => r.GetByBattleNetIdAsync("999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);
        repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        cipherMock
            .Setup(c => c.Protect(It.IsAny<SessionPrincipal>()))
            .Returns(FakeEncrypted);

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState },
            cookies: new() { ["login_state"] = "protected-payload" });

        // Act
        var result = await fn.Run(req, CancellationToken.None);

        // Assert: redirects to AppBaseUrl (not failure URL)
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(AppBaseUrl,
            "successful callback without a stored redirect must redirect to AppBaseUrl");
        redirect.Permanent.Should().BeFalse("OAuth callback redirects must be 302");

        // Assert: auth cookie set in response
        var responseHeaders = httpContext.Response.Headers;
        var setCookieHeaders = responseHeaders["Set-Cookie"].ToArray();
        setCookieHeaders.Should().Contain(h => h.Contains(FakeCookieName),
            "the auth cookie must be set in the response");
        setCookieHeaders.Should().Contain(h => h.Contains(FakeEncrypted),
            "the auth cookie must contain the encrypted session token");

        // Assert: login_state cookie cleared (MaxAge=0)
        setCookieHeaders.Should().Contain(h => h.Contains("login_state") && h.Contains("max-age=0"),
            "the login_state cookie must be cleared after successful callback");

        // Assert: repository upserted with correct battleNetId
        repoMock.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d => d.BattleNetId == "999"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_happy_path_with_redirect_appends_path_to_app_base_url()
    {
        // Arrange — redirect stored in login state
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        const string postLoginPath = "/runs/new";

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns((FakeState, FakeVerifier, (string?)postLoginPath));
        oauthMock
            .Setup(o => o.ExchangeCodeAsync(FakeCode, FakeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeTokenResponse);
        oauthMock
            .Setup(o => o.GetUserInfoAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeUser);

        repoMock
            .Setup(r => r.GetByBattleNetIdAsync("999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);
        repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        cipherMock
            .Setup(c => c.Protect(It.IsAny<SessionPrincipal>()))
            .Returns(FakeEncrypted);

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState },
            cookies: new() { ["login_state"] = "protected-payload" });

        // Act
        var result = await fn.Run(req, CancellationToken.None);

        // Assert: redirects to AppBaseUrl + redirect path
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be($"{AppBaseUrl}{postLoginPath}",
            "successful callback must redirect to AppBaseUrl + the stored redirect path");
        redirect.Permanent.Should().BeFalse("OAuth callback redirects must be 302");
    }

    // -----------------------------------------------------------------------
    // Invalid state
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_missing_login_state_cookie_redirects_to_failure()
    {
        // Arrange — no login_state cookie, state query param present
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState });
        // No cookie injected.

        // Act
        var result = await fn.Run(req, CancellationToken.None);

        // Assert: redirects to failure URL
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(FailureUrl,
            "missing login_state cookie must redirect to failure, not crash");
        redirect.Permanent.Should().BeFalse();

        // Assert: ExchangeCodeAsync was NOT called
        oauthMock.Verify(o => o.ExchangeCodeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_mismatched_state_redirects_to_failure()
    {
        // Arrange — cookie decodes to a different state than the query param
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns(("expected-state", FakeVerifier, (string?)null)); // state in cookie ≠ state in URL

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = "different-state" },
            cookies: new() { ["login_state"] = "some-protected-payload" });

        // Act
        var result = await fn.Run(req, CancellationToken.None);

        // Assert
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(FailureUrl,
            "state mismatch (possible CSRF) must redirect to failure");

        oauthMock.Verify(o => o.ExchangeCodeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -----------------------------------------------------------------------
    // Failed code exchange
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_failed_code_exchange_redirects_to_failure()
    {
        // Arrange — state validates OK but ExchangeCodeAsync throws
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns((FakeState, FakeVerifier, (string?)null));
        oauthMock
            .Setup(o => o.ExchangeCodeAsync(FakeCode, FakeVerifier, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Battle.net returned 400 Bad Request"));

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState },
            cookies: new() { ["login_state"] = "some-protected-payload" });

        // Act
        var result = await fn.Run(req, CancellationToken.None);

        // Assert: redirects to failure, no auth cookie set
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(FailureUrl,
            "failed token exchange must redirect to failure URL");

        // Assert: no auth cookie set in response
        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].ToArray();
        setCookieHeaders.Should().NotContain(h => h.Contains(FakeCookieName),
            "auth cookie must NOT be set when token exchange fails");

        cipherMock.Verify(c => c.Protect(It.IsAny<SessionPrincipal>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Audit events
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_happy_path_emits_login_success_audit_event()
    {
        // Arrange
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);
        var logger = new TestLogger<BattleNetCallbackFunction>();

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns((FakeState, FakeVerifier, (string?)null));
        oauthMock
            .Setup(o => o.ExchangeCodeAsync(FakeCode, FakeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeTokenResponse);
        oauthMock
            .Setup(o => o.GetUserInfoAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeUser);

        repoMock
            .Setup(r => r.GetByBattleNetIdAsync("999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);
        repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        cipherMock
            .Setup(c => c.Protect(It.IsAny<SessionPrincipal>()))
            .Returns(FakeEncrypted);

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock, logger);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState },
            cookies: new() { ["login_state"] = "protected-payload" });

        // Act
        await fn.Run(req, CancellationToken.None);

        // Assert: logger called with "login.success" and the battleNetId "999"
        logger.Entries.Should().ContainSingle(e => e.IsAudit(
            action: "login.success",
            actorId: "999",
            result: "success"),
            "happy path must emit a login.success audit event with the battleNetId");
    }

    // -----------------------------------------------------------------------
    // Cookie flags
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_happy_path_sets_auth_cookie_with_secure_httponly_samesite_lax_flags()
    {
        // Arrange — same as the happy path test
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);

        oauthMock
            .Setup(o => o.UnprotectLoginState(It.IsAny<string>()))
            .Returns((FakeState, FakeVerifier, (string?)null));
        oauthMock
            .Setup(o => o.ExchangeCodeAsync(FakeCode, FakeVerifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeTokenResponse);
        oauthMock
            .Setup(o => o.GetUserInfoAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeUser);
        repoMock
            .Setup(r => r.GetByBattleNetIdAsync("999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);
        repoMock
            .Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cipherMock
            .Setup(c => c.Protect(It.IsAny<SessionPrincipal>()))
            .Returns(FakeEncrypted);

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState },
            cookies: new() { ["login_state"] = "protected-payload" });

        // Act
        await fn.Run(req, CancellationToken.None);

        // Assert: every cookie set must carry the security-critical flags.
        // Locks down a real attack surface — Stryker found mutations on these flags surviving.
        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].OfType<string>().ToArray();
        var authCookie = setCookieHeaders.SingleOrDefault(h => h.Contains(FakeCookieName));
        authCookie.Should().NotBeNull("the auth cookie must appear in Set-Cookie");
        authCookie!.ToLowerInvariant().Should().Contain("secure", "auth cookie must be Secure");
        authCookie.ToLowerInvariant().Should().Contain("httponly", "auth cookie must be HttpOnly");
        authCookie.ToLowerInvariant().Should().Contain("samesite=lax", "auth cookie must be SameSite=Lax");
        authCookie.ToLowerInvariant().Should().Contain("path=/", "auth cookie must be path-scoped to /");
        authCookie.Should().StartWith(FakeCookieName + "=",
            "the cookie name must come from AuthOptions.CookieName, not a hardcoded string");
    }

    [Fact]
    public async Task Run_clears_login_state_cookie_with_secure_httponly_flags()
    {
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);
        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState });
        // No login_state cookie -> goes through RejectWithClearedCookie path.

        await fn.Run(req, CancellationToken.None);

        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].OfType<string>().ToArray();
        var loginStateCookie = setCookieHeaders.SingleOrDefault(h => h.Contains("login_state"));
        loginStateCookie.Should().NotBeNull();
        loginStateCookie!.ToLowerInvariant().Should().Contain("secure");
        loginStateCookie.ToLowerInvariant().Should().Contain("httponly");
        loginStateCookie.ToLowerInvariant().Should().Contain("samesite=lax");
        loginStateCookie.ToLowerInvariant().Should().Contain("max-age=0",
            "clearing the cookie requires Max-Age=0");
    }

    [Fact]
    public async Task Run_missing_cookie_emits_login_failure_audit_event()
    {
        // Arrange — no login_state cookie
        var oauthMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        var cipherMock = new Mock<ISessionCipher>(MockBehavior.Strict);
        var repoMock = new Mock<IRaidersRepository>(MockBehavior.Strict);
        var logger = new TestLogger<BattleNetCallbackFunction>();

        var (fn, httpContext) = MakeFunction(oauthMock, cipherMock, repoMock, logger);
        var req = BuildRequest(httpContext,
            query: new() { ["code"] = FakeCode, ["state"] = FakeState });

        // Act
        await fn.Run(req, CancellationToken.None);

        // Assert: logger called with "login.failure" and the missing-cookie detail
        logger.Entries.Should().ContainSingle(
            e => e.IsAudit("login.failure", "failure", "missing login_state or state"),
            "missing cookie must emit a login.failure audit event");
    }
}
