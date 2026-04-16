// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lfm.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IBlizzardOAuthClient"/> using Battle.net OAuth 2.0 endpoints.
///
/// State-handling approach (B2.2):
///   The TS implementation seals { state, codeVerifier } into a signed HS256 JWT stored
///   in a <c>login_state</c> HttpOnly cookie (5-min TTL). We use an IDataProtector
///   (purpose "Lfm.OAuth.LoginState.v1") for the same goal — tamper-evident, time-limited
///   payload — without adding a JWT dependency.
///
///   Login handler:
///     1. GenerateState() → random GUID ("N")
///     2. GenerateCodeVerifier() → random Base64url string
///     3. ComputeCodeChallenge(verifier) → base64url(SHA-256(verifier))
///     4. BuildAuthorizeUrl(state, codeChallenge) → Battle.net authorize URL
///     5. ProtectLoginState(state, verifier, redirect) → protected cookie payload
///
///   Callback handler:
///     1. UnprotectLoginState(cookiePayload) → (state, verifier, redirect)?
///     2. Validate query-param state == cookie state
///     3. ExchangeCodeAsync(code, verifier) → access token
///     4. GetUserInfoAsync(accessToken) → user id + battletag
/// </summary>
public sealed class BlizzardOAuthClient : IBlizzardOAuthClient
{
    private static readonly string LoginStatePurpose = "Lfm.OAuth.LoginState.v1";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly BlizzardOptions _opts;
    private readonly HttpClient _httpClient;
    private readonly IDataProtector _loginStateProtector;

    public BlizzardOAuthClient(
        HttpClient httpClient,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BlizzardOptions> options)
    {
        _opts = options.Value;
        _httpClient = httpClient;
        _loginStateProtector = dataProtectionProvider.CreateProtector(LoginStatePurpose);
    }

    /// <inheritdoc/>
    public string GenerateState() => Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string GenerateCodeVerifier()
    {
        // RFC 7636: 43-128 unreserved characters. Use 32 random bytes → 43 Base64url chars.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    /// <inheritdoc/>
    public string BuildAuthorizeUrl(string state, string codeChallenge)
    {
        if (string.IsNullOrEmpty(state))
            throw new ArgumentException("State must not be empty.", nameof(state));
        if (string.IsNullOrEmpty(codeChallenge))
            throw new ArgumentException("Code challenge must not be empty.", nameof(codeChallenge));

        var host = OAuthHost() + "/oauth/authorize";

        var qb = new QueryBuilder
        {
            { "response_type",        "code" },
            { "client_id",            _opts.ClientId },
            { "redirect_uri",         _opts.RedirectUri },
            { "scope",                "wow.profile" },
            { "state",                state },
            { "code_challenge",       codeChallenge },
            { "code_challenge_method","S256" }
        };

        return host + qb.ToQueryString();
    }

    /// <summary>
    /// Resolves the OAuth base host. Returns <see cref="BlizzardOptions.OAuthBaseUrl"/>
    /// when set (E2E test override) or the production region-specific Battle.net host.
    /// </summary>
    private string OAuthHost()
    {
        if (!string.IsNullOrEmpty(_opts.OAuthBaseUrl))
            return _opts.OAuthBaseUrl.TrimEnd('/');
        var region = _opts.Region.ToLowerInvariant();
        return $"https://{region}.battle.net";
    }

    /// <inheritdoc/>
    public string ProtectLoginState(string state, string codeVerifier, string? redirect)
    {
        // Serialize as "state:codeVerifier:redirect" (redirect may be empty).
        // Using '|' as the delimiter — it is not a valid URL character in paths,
        // so it cannot appear in state, codeVerifier, or a relative redirect path.
        // The protector adds authenticated encryption + expiry via SetApplicationName.
        var encodedRedirect = redirect ?? string.Empty;
        var payload = $"{state}|{codeVerifier}|{encodedRedirect}";
        return _loginStateProtector.Protect(payload);
    }

    /// <inheritdoc/>
    public (string state, string codeVerifier, string? redirect)? UnprotectLoginState(string payload)
    {
        try
        {
            var raw = _loginStateProtector.Unprotect(payload);
            // Expected format: "state|codeVerifier|redirect"
            var parts = raw.Split('|');
            if (parts.Length < 2) return null;
            var state = parts[0];
            var codeVerifier = parts[1];
            // redirect is the third segment; absent or empty → null
            var redirect = parts.Length >= 3 && !string.IsNullOrEmpty(parts[2])
                ? parts[2]
                : null;
            if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(codeVerifier))
                return null;
            return (state, codeVerifier, redirect);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<BlizzardTokenResponse> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var tokenUrl = OAuthHost() + "/oauth/token";

        var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _opts.RedirectUri,
            ["code_verifier"] = codeVerifier,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = requestBody,
        };

        // Basic auth: Authorization: Basic base64(clientId:clientSecret)
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_opts.ClientId}:{_opts.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenDto = JsonSerializer.Deserialize<TokenEndpointResponse>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Battle.net token endpoint returned empty response.");

        return new BlizzardTokenResponse(tokenDto.AccessToken, tokenDto.ExpiresIn);
    }

    /// <inheritdoc/>
    public async Task<BlizzardUserInfo> GetUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var userInfoUrl = OAuthHost() + "/oauth/userinfo";

        var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var userDto = JsonSerializer.Deserialize<UserInfoEndpointResponse>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Battle.net userinfo endpoint returned empty response.");

        return new BlizzardUserInfo(userDto.Id, userDto.BattleTag);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>Computes a PKCE S256 code challenge from a verifier.</summary>
    public static string ComputeCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
               .TrimEnd('=')
               .Replace('+', '-')
               .Replace('/', '_');

    // ---------------------------------------------------------------------------
    // Private DTOs — not part of the public contract
    // ---------------------------------------------------------------------------

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed class UserInfoEndpointResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("battletag")]
        public string BattleTag { get; init; } = string.Empty;
    }
}
