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
/// the redirect response is assembled correctly.
/// </summary>
public class BattleNetLoginFunctionTests
{
    private const string FakeState = "abc123statexyz";
    private const string FakeAuthUrl = "https://eu.battle.net/oauth/authorize?response_type=code&client_id=test&redirect_uri=https%3A%2F%2Fexample.com%2Fcb&scope=wow.profile&state=abc123statexyz";

    private static BattleNetLoginFunction MakeFunction(out Mock<IBlizzardOAuthClient> clientMock)
    {
        clientMock = new Mock<IBlizzardOAuthClient>(MockBehavior.Strict);
        clientMock.Setup(c => c.GenerateState()).Returns(FakeState);
        clientMock.Setup(c => c.BuildAuthorizeUrl(FakeState)).Returns(FakeAuthUrl);
        return new BattleNetLoginFunction(clientMock.Object);
    }

    [Fact]
    public async Task Run_redirects_to_battle_net_authorize_url()
    {
        var fn = MakeFunction(out _);
        var req = new DefaultHttpContext().Request;

        var result = await Task.FromResult(fn.Run(req, CancellationToken.None));

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().StartWith("https://eu.battle.net/oauth/authorize",
            "the redirect target must be the Battle.net authorization endpoint");
        redirect.Permanent.Should().BeFalse("OAuth login redirects must be 302, not 301");
    }

    [Fact]
    public async Task Run_redirect_url_contains_non_empty_state_parameter()
    {
        var fn = MakeFunction(out _);
        var req = new DefaultHttpContext().Request;

        var result = await Task.FromResult(fn.Run(req, CancellationToken.None));

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        var uri = new Uri(redirect.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        query["state"].Should().NotBeNullOrEmpty(
            "state is required to prevent CSRF attacks on the callback");
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
