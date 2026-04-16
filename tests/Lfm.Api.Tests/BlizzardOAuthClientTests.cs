// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("eu.battle.net", uri.Host);
        Assert.Equal("/oauth/authorize", uri.AbsolutePath);

        Assert.Equal("code", query["response_type"]);
        Assert.Equal("test-client", query["client_id"]);
        Assert.Equal("https://example.com/api/battlenet/callback", query["redirect_uri"]);
        Assert.Equal("wow.profile", query["scope"]);
        Assert.Equal(state, query["state"]);
        Assert.Equal(codeChallenge, query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
    }

    [Fact]
    public void GenerateState_returns_non_empty_value_preventing_csrf()
    {
        var client = MakeClient();

        var state = client.GenerateState();

        Assert.False(string.IsNullOrEmpty(state));
        // 32 hex chars = GUID without hyphens ("N" format)
        Assert.Equal(32, state.Length);
        Assert.Matches("^[0-9a-f]{32}$", state);
    }

    [Fact]
    public void GenerateCodeVerifier_returns_base64url_string()
    {
        var client = MakeClient();

        var verifier = client.GenerateCodeVerifier();

        Assert.False(string.IsNullOrEmpty(verifier));
        // Base64url: only A-Z, a-z, 0-9, '-', '_' (no '+', '/', '=')
        Assert.Matches(@"^[A-Za-z0-9\-_]+$", verifier);
        Assert.True(verifier.Length >= 43);
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

        Assert.Equal(expectedHost, new Uri(url).Host);
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
        Assert.Equal("localhost", uri.Host);
        Assert.Equal(9999, uri.Port);
        Assert.Equal("/oauth/authorize", uri.AbsolutePath);
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

        Assert.DoesNotContain("//oauth/", url);
        Assert.Contains("/oauth/authorize", url);
    }

    [Fact]
    public void ProtectLoginState_then_UnprotectLoginState_round_trips_without_redirect()
    {
        var client = MakeClient();
        var state = client.GenerateState();
        var verifier = client.GenerateCodeVerifier();

        var payload = client.ProtectLoginState(state, verifier, redirect: null);
        var result = client.UnprotectLoginState(payload);

        Assert.NotNull(result);
        Assert.Equal(state, result!.Value.state);
        Assert.Equal(verifier, result.Value.codeVerifier);
        Assert.Null(result.Value.redirect);
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

        Assert.NotNull(result);
        Assert.Equal(state, result!.Value.state);
        Assert.Equal(verifier, result.Value.codeVerifier);
        Assert.Equal(redirect, result.Value.redirect);
    }

    [Fact]
    public void UnprotectLoginState_returns_null_for_tampered_payload()
    {
        var client = MakeClient();

        var result = client.UnprotectLoginState("tampered.garbage.payload");

        Assert.Null(result);
    }

    [Fact]
    public void ComputeCodeChallenge_produces_base64url_sha256()
    {
        // RFC 7636 §4.6: BASE64URL(SHA256(ASCII(code_verifier)))
        // Known vector: verifier "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        // → challenge "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expected = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.Equal(expected, BlizzardOAuthClient.ComputeCodeChallenge(verifier));
    }
}
