using FluentAssertions;
using Lfm.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using BlizzardOptions = Lfm.Api.Options.BlizzardOptions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BlizzardOAuthClient"/>.
/// These tests exercise the pure URL-building and state-protection logic
/// directly on the service, which is easier and faster than going through
/// the HTTP trigger.
/// </summary>
public class BlizzardOAuthClientTests
{
    private static BlizzardOAuthClient MakeClient(
        string clientId = "test-client",
        string region = "eu",
        string redirectUri = "https://example.com/api/battlenet/callback",
        string? oauthBaseUrl = null)
    {
        var opts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = clientId,
            ClientSecret = "secret",
            Region = region,
            RedirectUri = redirectUri,
            AppBaseUrl = "https://example.com",
            OAuthBaseUrl = oauthBaseUrl,
        });

        // Use ephemeral Data Protection for unit tests (no Azure storage required).
        var dpProvider = new EphemeralDataProtectionProvider();

        return new BlizzardOAuthClient(
            new HttpClient(),
            dpProvider,
            opts);
    }

    [Fact]
    public void BuildAuthorizeUrl_contains_all_required_oauth_parameters()
    {
        var client = MakeClient();
        var state = client.GenerateState();
        var codeVerifier = client.GenerateCodeVerifier();
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(codeVerifier);

        var url = client.BuildAuthorizeUrl(state, codeChallenge);

        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        uri.Scheme.Should().Be("https");
        uri.Host.Should().Be("eu.battle.net");
        uri.AbsolutePath.Should().Be("/oauth/authorize");

        query["response_type"].Should().Be("code", "OAuth code grant requires response_type=code");
        query["client_id"].Should().Be("test-client", "client_id must match BlizzardOptions.ClientId");
        query["redirect_uri"].Should().Be("https://example.com/api/battlenet/callback");
        query["scope"].Should().Be("wow.profile", "WoW profile scope is required");
        query["state"].Should().Be(state, "state must round-trip intact");
        query["code_challenge"].Should().Be(codeChallenge, "PKCE code_challenge must be present");
        query["code_challenge_method"].Should().Be("S256", "PKCE S256 method is required");
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

    [Fact]
    public void GenerateCodeVerifier_returns_base64url_string()
    {
        var client = MakeClient();

        var verifier = client.GenerateCodeVerifier();

        verifier.Should().NotBeNullOrEmpty();
        // Base64url: only A-Z, a-z, 0-9, '-', '_' (no '+', '/', '=')
        verifier.Should().MatchRegex(@"^[A-Za-z0-9\-_]+$",
            "code verifier must be Base64url-encoded (no padding, no +/)");
        verifier.Length.Should().BeGreaterThanOrEqualTo(43,
            "RFC 7636 requires at least 43 characters for 256-bit entropy");
    }

    [Theory]
    [InlineData("eu", "eu.battle.net")]
    [InlineData("us", "us.battle.net")]
    [InlineData("EU", "eu.battle.net")]   // region is normalised to lower-case
    public void BuildAuthorizeUrl_uses_correct_regional_host(string region, string expectedHost)
    {
        var client = MakeClient(region: region);
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());
        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        new Uri(url).Host.Should().Be(expectedHost);
    }

    [Fact]
    public void BuildAuthorizeUrl_uses_OAuthBaseUrl_override_when_set()
    {
        // The override allows the E2E stack to point the OAuth client at a
        // local WireMock stub instead of the real Battle.net host. Production
        // leaves the option unset; tests set it via Blizzard__OAuthBaseUrl.
        var client = MakeClient(
            region: "eu",
            oauthBaseUrl: "http://localhost:9999");
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());

        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        var uri = new Uri(url);
        uri.Host.Should().Be("localhost", "the override must replace the regional Battle.net host");
        uri.Port.Should().Be(9999, "the override port must be honoured");
        uri.AbsolutePath.Should().Be("/oauth/authorize",
            "the override must preserve the OAuth path layout");
    }

    [Fact]
    public void BuildAuthorizeUrl_strips_trailing_slash_from_OAuthBaseUrl_override()
    {
        // A trailing slash in the override would produce double-slash URLs
        // like http://localhost:9999//oauth/authorize, which some HTTP servers
        // (including WireMock) treat as a different path. Pin the trim.
        var client = MakeClient(oauthBaseUrl: "http://localhost:9999/");
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());

        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        url.Should().NotContain("//oauth/", "trailing slash in the override must be trimmed");
        url.Should().Contain("/oauth/authorize");
    }

    [Fact]
    public void ProtectLoginState_then_UnprotectLoginState_round_trips_without_redirect()
    {
        var client = MakeClient();
        var state = client.GenerateState();
        var verifier = client.GenerateCodeVerifier();

        var payload = client.ProtectLoginState(state, verifier, redirect: null);
        var result = client.UnprotectLoginState(payload);

        result.Should().NotBeNull("valid protected payload must round-trip");
        result!.Value.state.Should().Be(state);
        result.Value.codeVerifier.Should().Be(verifier);
        result.Value.redirect.Should().BeNull("no redirect was stored");
    }

    [Fact]
    public void ProtectLoginState_then_UnprotectLoginState_round_trips_with_redirect()
    {
        var client = MakeClient();
        var state = client.GenerateState();
        var verifier = client.GenerateCodeVerifier();
        const string redirect = "/runs/new";

        var payload = client.ProtectLoginState(state, verifier, redirect);
        var result = client.UnprotectLoginState(payload);

        result.Should().NotBeNull("valid protected payload must round-trip");
        result!.Value.state.Should().Be(state);
        result.Value.codeVerifier.Should().Be(verifier);
        result.Value.redirect.Should().Be(redirect, "redirect path must survive the round-trip");
    }

    [Fact]
    public void UnprotectLoginState_returns_null_for_tampered_payload()
    {
        var client = MakeClient();

        var result = client.UnprotectLoginState("tampered.garbage.payload");

        result.Should().BeNull("tampered payloads must be rejected");
    }

    [Fact]
    public void ComputeCodeChallenge_produces_base64url_sha256()
    {
        // RFC 7636 §4.6: BASE64URL(SHA256(ASCII(code_verifier)))
        // Known vector: verifier "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        // → challenge "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expected = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        BlizzardOAuthClient.ComputeCodeChallenge(verifier).Should().Be(expected);
    }
}
