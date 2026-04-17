// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.DataProtection;

namespace Lfm.Api.Services;

/// <summary>
/// Blizzard OAuth client. Split into what is needed task-by-task:
///   B2.1 (login)    — <see cref="GenerateState"/> + <see cref="GenerateCodeVerifier"/>
///                     + <see cref="BuildAuthorizeUrl"/> + <see cref="ProtectLoginState"/>
///   B2.2 (callback) — <see cref="UnprotectLoginState"/> + <see cref="ExchangeCodeAsync"/>
///                     + <see cref="GetUserInfoAsync"/>
///   B2.3 (logout)   — no additional methods needed
/// Design choice: single interface rather than splitting into multiple
/// partial-capability interfaces, because all three tasks share the same
/// underlying configuration (BlizzardOptions) and the interface stays small.
/// </summary>
public interface IBlizzardOAuthClient
{
    /// <summary>
    /// Generates a cryptographically random, URL-safe state token for use as
    /// the OAuth <c>state</c> parameter (CSRF protection).
    /// Returns a random GUID ("N" format, 32 hex chars).
    /// </summary>
    string GenerateState();

    /// <summary>
    /// Generates a cryptographically random PKCE code verifier (RFC 7636).
    /// </summary>
    string GenerateCodeVerifier();

    /// <summary>
    /// Builds the Battle.net OAuth authorization URL including all required
    /// query parameters: client_id, redirect_uri, response_type=code,
    /// scope=wow.profile, state, code_challenge, code_challenge_method=S256.
    /// </summary>
    /// <param name="state">A non-empty CSRF state token (see <see cref="GenerateState"/>).</param>
    /// <param name="codeChallenge">Base64url-encoded SHA-256 hash of the code verifier (PKCE S256).</param>
    /// <returns>The fully-formed authorization URL string.</returns>
    string BuildAuthorizeUrl(string state, string codeChallenge);

    /// <summary>
    /// Seals the login state (state + codeVerifier + redirect) into a tamper-evident
    /// payload using Data Protection. Used by the login handler to set the
    /// <c>login_state</c> cookie.
    /// </summary>
    /// <param name="state">A non-empty CSRF state token.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <param name="redirect">
    /// Optional relative path to redirect to after a successful login (e.g. "/runs/new").
    /// Must start with "/" and must NOT start with "//". Null or empty means no post-login redirect.
    /// </param>
    string ProtectLoginState(string state, string codeVerifier, string? redirect);

    /// <summary>
    /// Unseals and validates a <c>login_state</c> cookie payload previously
    /// created by <see cref="ProtectLoginState"/>.
    /// Returns null if the payload is invalid, tampered, or expired.
    /// </summary>
    (string state, string codeVerifier, string? redirect)? UnprotectLoginState(string payload);

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// </summary>
    Task<BlizzardTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the authenticated user's profile from the Battle.net userinfo endpoint.
    /// </summary>
    Task<BlizzardUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>Token response from the Battle.net OAuth token endpoint.</summary>
/// <param name="AccessToken">The Bearer access token.</param>
/// <param name="ExpiresIn">Lifetime in seconds.</param>
public sealed record BlizzardTokenResponse(string AccessToken, int ExpiresIn);

/// <summary>User identity returned by the Battle.net userinfo endpoint.</summary>
/// <param name="Id">Battle.net account identifier (numeric).</param>
/// <param name="BattleTag">Human-readable BattleTag, e.g. "Player#1234".</param>
public sealed record BlizzardUserInfo(long Id, string BattleTag);
