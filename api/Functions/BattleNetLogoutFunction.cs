using Lfm.Api.Auth;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// POST /api/battlenet/logout — clears the auth cookie and redirects to the home page.
///
/// Auth: [RequireAuth] — the user must be logged in to log out.
///
/// Behavior:
///   1. Delete the auth cookie (set to expired).
///   2. Redirect to BlizzardOptions.AppBaseUrl.
/// </summary>
public class BattleNetLogoutFunction(
    IOptions<BlizzardOptions> blizzardOptions,
    IOptions<AuthOptions> authOptions)
{
    private readonly BlizzardOptions _blizzardOpts = blizzardOptions.Value;
    private readonly AuthOptions _authOpts = authOptions.Value;

    [RequireAuth]
    [Function("battlenet-logout")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "battlenet/logout")] HttpRequest req,
        FunctionContext ctx)
    {
        // Clear the auth cookie by deleting it (MaxAge=0).
        req.HttpContext.Response.Cookies.Delete(_authOpts.CookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = req.IsHttps,
        });

        // Redirect to the home page.
        return new RedirectResult(_blizzardOpts.AppBaseUrl, permanent: false);
    }
}
