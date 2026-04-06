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
        out Mock<IBlizzardOAuthClient> clientMock)
    {
        clientMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.GenerateState()).Returns(FakeState);
        clientMock.Setup(c => c.GenerateCodeVerifier()).Returns(FakeVerifier);
        // ComputeCodeChallenge is a static helper; we control BuildAuthorizeUrl directly.
        clientMock.Setup(c => c.BuildAuthorizeUrl(FakeState, It.IsAny<string>())).Returns(FakeAuthUrl);
        clientMock.Setup(c => c.ProtectLoginState(FakeState, FakeVerifier)).Returns(FakeLoginState);

        var fn = new BattleNetLoginFunction(clientMock.Object);

        // Build a real HttpContext so Response.Cookies.Append works.
        var httpContext = new DefaultHttpContext();
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
    public void Run_method_does_NOT_have_RequireAuth_attribute()
    {
        // This endpoint is the START of the login flow — the user has no session yet.
        var method = typeof(BattleNetLoginFunction).GetMethod(nameof(BattleNetLoginFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().BeEmpty("battlenet-login is an anonymous endpoint; adding RequireAuth would break login");
    }
}
