// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
        string? oauthBaseUrl = null,
        string? authorizationEndpoint = null,
        string? tokenEndpoint = null,
        string? userInfoEndpoint = null,
        HttpClient? httpClient = null)
    {
        var opts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = clientId,
            ClientSecret = "secret",
            Region = region,
            RedirectUri = redirectUri,
            AppBaseUrl = "https://example.com",
            OAuthBaseUrl = oauthBaseUrl,
            AuthorizationEndpoint = authorizationEndpoint,
            TokenEndpoint = tokenEndpoint,
            UserInfoEndpoint = userInfoEndpoint,
        });

        // Use ephemeral Data Protection for unit tests (no Azure storage required).
        var dpProvider = new EphemeralDataProtectionProvider();

        return new BlizzardOAuthClient(
            httpClient ?? new HttpClient(),
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
    public void BuildAuthorizeUrl_uses_authorization_endpoint_override_when_set()
    {
        var client = MakeClient(
            authorizationEndpoint: "http://localhost:18080/bnet/authorize");
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());

        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        var uri = new Uri(url);
        Assert.Equal("localhost", uri.Host);
        Assert.Equal(18080, uri.Port);
        Assert.Equal("/bnet/authorize", uri.AbsolutePath);
    }

    [Fact]
    public void BuildAuthorizeUrl_preserves_trailing_slash_in_authorization_endpoint_override()
    {
        var client = MakeClient(
            authorizationEndpoint: "http://localhost:18080/bnet/authorize/");
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());

        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        Assert.Equal("/bnet/authorize/", new Uri(url).AbsolutePath);
    }

    [Fact]
    public void BuildAuthorizeUrl_prefers_authorization_endpoint_over_OAuthBaseUrl()
    {
        var client = MakeClient(
            oauthBaseUrl: "http://localhost:9999",
            authorizationEndpoint: "http://localhost:18080/bnet/authorize");
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(client.GenerateCodeVerifier());

        var url = client.BuildAuthorizeUrl(client.GenerateState(), codeChallenge);

        Assert.Equal("/bnet/authorize", new Uri(url).AbsolutePath);
    }

    [Fact]
    public async Task ExchangeCodeAsync_uses_token_endpoint_override_when_set()
    {
        using var handler = new RecordingHandler(
            """{"access_token":"access-123","token_type":"Bearer","expires_in":3600}""");
        using var httpClient = new HttpClient(handler);
        var client = MakeClient(
            tokenEndpoint: "http://host.docker.internal:18080/bnet/token",
            httpClient: httpClient);

        var token = await client.ExchangeCodeAsync("code-123", "verifier-123");

        Assert.Equal("access-123", token.AccessToken);
        Assert.Equal("http://host.docker.internal:18080/bnet/token", handler.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.NotNull(handler.Authorization);
        Assert.Equal("Basic", handler.Authorization!.Scheme);
        Assert.Contains("code=code-123", handler.Body);
        Assert.Contains("code_verifier=verifier-123", handler.Body);
    }

    [Fact]
    public async Task GetUserInfoAsync_uses_userinfo_endpoint_override_when_set()
    {
        using var handler = new RecordingHandler(
            """{"id":987654321,"battletag":"OAuthTest#1234"}""");
        using var httpClient = new HttpClient(handler);
        var client = MakeClient(
            userInfoEndpoint: "http://host.docker.internal:18080/bnet/userinfo",
            httpClient: httpClient);

        var user = await client.GetUserInfoAsync("access-123");

        Assert.Equal(987654321, user.Id);
        Assert.Equal("OAuthTest#1234", user.BattleTag);
        Assert.Equal("http://host.docker.internal:18080/bnet/userinfo", handler.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-123", handler.Authorization.Parameter);
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

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler, IDisposable
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            Authorization = request.Headers.Authorization;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
