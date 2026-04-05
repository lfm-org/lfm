using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// GET /api/battlenet/login — redirects the browser to the Battle.net OAuth
/// authorization page. This endpoint is intentionally unauthenticated: the
/// user starts here before they have a session. No [RequireAuth] attribute.
/// </summary>
public class BattleNetLoginFunction(IBlizzardOAuthClient oauthClient)
{
    [Function("battlenet-login")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/login")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var state = oauthClient.GenerateState();
        var authUrl = oauthClient.BuildAuthorizeUrl(state);

        // B2.2 will enhance this to set the login_state cookie (sealed PKCE payload)
        // so the callback can validate state and exchange the code. For B2.1 we just
        // redirect — matching the happy path of the TS handler when no cookie domain
        // or test scenario is involved.
        return new RedirectResult(authUrl, permanent: false);
    }
}
