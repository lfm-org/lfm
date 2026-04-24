// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// GET /api/battlenet/login — redirects the browser to the Battle.net OAuth
/// authorization page. This endpoint is intentionally unauthenticated: the
/// user starts here before they have a session. No [RequireAuth] attribute.
///
/// PKCE flow (B2.2):
///   1. Read and validate the optional <c>redirect</c> query parameter.
///   2. Generate a random state (CSRF protection).
///   3. Generate a random PKCE code verifier.
///   4. Compute the S256 code challenge from the verifier.
///   5. Seal {state, codeVerifier, redirect} into the login_state cookie via IDataProtector.
///   6. Build the Battle.net authorize URL including code_challenge.
///   7. Set the login_state HttpOnly cookie (5-minute TTL) and redirect.
/// </summary>
public class BattleNetLoginFunction(IBlizzardOAuthClient oauthClient)
{
    private const int LoginStateCookieMaxAge = 5 * 60; // 5 minutes in seconds

    [Function("battlenet-login")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/login")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        // Read and validate the post-login redirect path.
        // Security: must be a relative path (starts with "/") but not protocol-relative ("//").
        var redirectParam = req.Query["redirect"].FirstOrDefault();
        var redirect = IsValidRedirect(redirectParam) ? redirectParam : null;

        var state = oauthClient.GenerateState();
        var codeVerifier = oauthClient.GenerateCodeVerifier();
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(codeVerifier);
        var authUrl = oauthClient.BuildAuthorizeUrl(state, codeChallenge);
        var loginStatePayload = oauthClient.ProtectLoginState(state, codeVerifier, redirect);

        // Set the login_state cookie: HttpOnly, Secure, SameSite=Lax, 5-min TTL.
        req.HttpContext.Response.Cookies.Append("login_state", loginStatePayload, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(LoginStateCookieMaxAge),
        });

        return new RedirectResult(authUrl, permanent: false);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/login</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-login-v1")]
    public IActionResult RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/battlenet/login")] HttpRequest req,
        CancellationToken cancellationToken)
        => Run(req, cancellationToken);

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns true if <paramref name="redirect"/> is a safe relative path:
    /// starts with "/" but NOT "//" (protocol-relative URL).
    /// </summary>
    internal static bool IsValidRedirect(string? redirect)
        => !string.IsNullOrEmpty(redirect)
           && redirect.StartsWith('/')
           && !redirect.StartsWith("//");
}
