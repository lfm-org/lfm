using FluentAssertions;
using Lfm.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

using BlizzardOptions = Lfm.Api.Options.BlizzardOptions;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BlizzardOAuthClient"/>.
/// These tests exercise the pure URL-building logic directly on the service,
/// which is easier and faster than going through the HTTP trigger.
/// </summary>
public class BlizzardOAuthClientTests
{
    private static IBlizzardOAuthClient MakeClient(
        string clientId = "test-client",
        string region = "eu",
        string redirectUri = "https://example.com/api/battlenet/callback")
    {
        var opts = Microsoft.Extensions.Options.Options.Create(new BlizzardOptions
        {
            ClientId = clientId,
            ClientSecret = "secret",
            Region = region,
            RedirectUri = redirectUri,
            AppBaseUrl = "https://example.com",
        });
        return new BlizzardOAuthClient(opts);
    }

    [Fact]
    public void BuildAuthorizeUrl_contains_all_required_oauth_parameters()
    {
        var client = MakeClient();
        var state = client.GenerateState();

        var url = client.BuildAuthorizeUrl(state);

        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        uri.Scheme.Should().Be("https");
        uri.Host.Should().Be("eu.battle.net");
        uri.AbsolutePath.Should().Be("/oauth/authorize");

        query["response_type"].Should().Be("code",    "OAuth code grant requires response_type=code");
        query["client_id"].Should().Be("test-client", "client_id must match BlizzardOptions.ClientId");
        query["redirect_uri"].Should().Be("https://example.com/api/battlenet/callback");
        query["scope"].Should().Be("wow.profile",     "WoW profile scope is required");
        query["state"].Should().Be(state,             "state must round-trip intact");
    }

    [Fact]
    public void GenerateState_returns_non_empty_value_preventing_csrf()
    {
        var client = MakeClient();

        var state = client.GenerateState();

        state.Should().NotBeNullOrEmpty("an empty state is equivalent to no CSRF protection");
        // 32 hex chars = GUID without hyphens ("N" format)
        state.Length.Should().Be(32);
        state.Should().MatchRegex("^[0-9a-f]{32}$", "state should be lowercase hex");
    }

    [Theory]
    [InlineData("eu",  "eu.battle.net")]
    [InlineData("us",  "us.battle.net")]
    [InlineData("EU",  "eu.battle.net")]   // region is normalised to lower-case
    public void BuildAuthorizeUrl_uses_correct_regional_host(string region, string expectedHost)
    {
        var client = MakeClient(region: region);
        var url = client.BuildAuthorizeUrl(client.GenerateState());

        new Uri(url).Host.Should().Be(expectedHost);
    }
}
