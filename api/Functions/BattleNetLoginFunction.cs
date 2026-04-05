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
///   1. Generate a random state (CSRF protection).
///   2. Generate a random PKCE code verifier.
///   3. Compute the S256 code challenge from the verifier.
///   4. Seal {state, codeVerifier} into the login_state cookie via IDataProtector.
///   5. Build the Battle.net authorize URL including code_challenge.
///   6. Set the login_state HttpOnly cookie (5-minute TTL) and redirect.
/// </summary>
public class BattleNetLoginFunction(IBlizzardOAuthClient oauthClient)
{
    private const int LoginStateCookieMaxAge = 5 * 60; // 5 minutes in seconds

    [Function("battlenet-login")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/login")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var state = oauthClient.GenerateState();
        var codeVerifier = oauthClient.GenerateCodeVerifier();
        var codeChallenge = BlizzardOAuthClient.ComputeCodeChallenge(codeVerifier);
        var authUrl = oauthClient.BuildAuthorizeUrl(state, codeChallenge);
        var loginStatePayload = oauthClient.ProtectLoginState(state, codeVerifier);

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
}
