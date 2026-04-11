using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BattleNetLoginFunction"/>.
/// The handler is a thin shell: it delegates URL construction to
/// <see cref="IBlizzardOAuthClient"/>, so handler tests only verify that
/// the redirect response is assembled correctly and the login_state cookie is set.
/// </summary>
public class BattleNetLoginFunctionTests
{
    private const string FakeState = "abc123statexyz";
    private const string FakeVerifier = "fake-code-verifier-abc";
    private const string FakeChallenge = "fake-code-challenge-xyz";
    private const string FakeLoginState = "protected-login-state-payload";
    private const string FakeAuthUrl =
        "https://eu.battle.net/oauth/authorize?response_type=code&client_id=test"
        + "&redirect_uri=https%3A%2F%2Fexample.com%2Fcb&scope=wow.profile"
        + "&state=abc123statexyz&code_challenge=fake-code-challenge-xyz&code_challenge_method=S256";

    private static (BattleNetLoginFunction fn, HttpContext httpContext) MakeFunction(
        out Mock<IBlizzardOAuthClient> clientMock,
        string? redirect = null)
    {
        clientMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.GenerateState()).Returns(FakeState);
        clientMock.Setup(c => c.GenerateCodeVerifier()).Returns(FakeVerifier);
        // ComputeCodeChallenge is a static helper; we control BuildAuthorizeUrl directly.
        clientMock.Setup(c => c.BuildAuthorizeUrl(FakeState, It.IsAny<string>())).Returns(FakeAuthUrl);
        clientMock.Setup(c => c.ProtectLoginState(FakeState, FakeVerifier, It.IsAny<string?>())).Returns(FakeLoginState);

        var fn = new BattleNetLoginFunction(clientMock.Object);

        // Build a real HttpContext so Response.Cookies.Append works.
        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrEmpty(redirect))
        {
            httpContext.Request.QueryString = new QueryString($"?redirect={Uri.EscapeDataString(redirect)}");
        }
        return (fn, httpContext);
    }

    [Fact]
    public void Run_redirects_to_battle_net_authorize_url()
    {
        var (fn, httpContext) = MakeFunction(out _);
        var req = httpContext.Request;

        var result = fn.Run(req, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().StartWith("https://eu.battle.net/oauth/authorize",
            "the redirect target must be the Battle.net authorization endpoint");
        redirect.Permanent.Should().BeFalse("OAuth login redirects must be 302, not 301");
    }

    [Fact]
    public void Run_redirect_url_contains_non_empty_state_parameter()
    {
        var (fn, httpContext) = MakeFunction(out _);
        var req = httpContext.Request;

        var result = fn.Run(req, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        var uri = new Uri(redirect.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["state"].Should().NotBeNullOrEmpty(
            "state is required to prevent CSRF attacks on the callback");
    }

    [Fact]
    public void Run_sets_login_state_cookie_with_secure_httponly_samesite_lax_flags()
    {
        var (fn, httpContext) = MakeFunction(out _);
        var req = httpContext.Request;

        fn.Run(req, CancellationToken.None);

        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].OfType<string>().ToArray();
        var loginStateCookie = setCookieHeaders.SingleOrDefault(h => h.Contains("login_state"));
        loginStateCookie.Should().NotBeNull("login_state must be set on login redirect");
        loginStateCookie!.ToLowerInvariant().Should().Contain("secure");
        loginStateCookie.ToLowerInvariant().Should().Contain("httponly");
        loginStateCookie.ToLowerInvariant().Should().Contain("samesite=lax");
        loginStateCookie.ToLowerInvariant().Should().Contain("path=/");
        loginStateCookie.ToLowerInvariant().Should().Contain("max-age=300",
            "login_state TTL must be 5 minutes (300 seconds)");
        loginStateCookie.Should().StartWith("login_state=",
            "the cookie name is the literal 'login_state' string");
    }

    [Fact]
    public void Run_redirect_url_contains_pkce_code_challenge()
    {
        var (fn, httpContext) = MakeFunction(out _);
        var req = httpContext.Request;

        var result = fn.Run(req, CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        var uri = new Uri(redirect.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["code_challenge"].Should().NotBeNullOrEmpty(
            "PKCE code_challenge must be included in the authorize URL");
        query["code_challenge_method"].Should().Be("S256",
            "PKCE S256 method is required per RFC 7636");
    }

    [Fact]
    public void Run_passes_valid_redirect_to_ProtectLoginState()
    {
        // Arrange: supply a valid relative redirect path
        const string redirectPath = "/runs/new";
        var (fn, httpContext) = MakeFunction(out var clientMock, redirect: redirectPath);
        var req = httpContext.Request;

        // Act
        fn.Run(req, CancellationToken.None);

        // Assert: ProtectLoginState was called with the redirect path
        clientMock.Verify(c => c.ProtectLoginState(FakeState, FakeVerifier, redirectPath), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("//evil.com")]
    [InlineData("https://evil.com")]
    [InlineData("evil")]
    public void Run_passes_null_redirect_to_ProtectLoginState_for_invalid_values(string? badRedirect)
    {
        // Arrange: supply an invalid or missing redirect value
        var clientMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.GenerateState()).Returns(FakeState);
        clientMock.Setup(c => c.GenerateCodeVerifier()).Returns(FakeVerifier);
        clientMock.Setup(c => c.BuildAuthorizeUrl(FakeState, It.IsAny<string>())).Returns(FakeAuthUrl);
        clientMock.Setup(c => c.ProtectLoginState(FakeState, FakeVerifier, null)).Returns(FakeLoginState);

        var fn = new BattleNetLoginFunction(clientMock.Object);
        var httpContext = new DefaultHttpContext();
        if (badRedirect is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?redirect={Uri.EscapeDataString(badRedirect)}");
        }

        // Act
        fn.Run(httpContext.Request, CancellationToken.None);

        // Assert: ProtectLoginState called with null redirect (open-redirect rejected)
        clientMock.Verify(c => c.ProtectLoginState(FakeState, FakeVerifier, null), Times.Once);
    }

    [Theory]
    [InlineData("/runs/new", true)]
    [InlineData("/", true)]
    [InlineData("/characters", true)]
    [InlineData("//evil.com", false)]
    [InlineData("https://evil.com", false)]
    [InlineData("evil", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidRedirect_accepts_only_relative_paths_that_are_not_protocol_relative(
        string? input, bool expected)
    {
        BattleNetLoginFunction.IsValidRedirect(input).Should().Be(expected);
    }
}
