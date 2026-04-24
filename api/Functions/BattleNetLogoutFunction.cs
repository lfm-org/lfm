// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// GET /api/battlenet/logout — clears the auth cookie and redirects to the home page.
///
/// Invoked by the Blazor client via a top-level <c>Nav.NavigateTo(..., forceLoad: true)</c>
/// (mirroring the <see cref="BattleNetLoginFunction"/> pattern). Using GET + top-level
/// navigation keeps the cookie-clear Set-Cookie response header processed by the browser
/// as part of a real navigation — no <c>HttpClient</c> credentials / timeout / cross-origin
/// fetch semantics to debug. An earlier POST-via-<c>HttpClient.PostAsync</c> implementation
/// swallowed timeouts silently and left the session alive on CI cold-starts (issue #53).
///
/// Anonymous: logging out when you are not logged in is a no-op. Rejecting unauthenticated
/// callers with 401 would produce a raw error page for any stale nav or bookmark. The
/// operation is idempotent and non-destructive.
///
/// Behavior:
///   1. Delete the auth cookie (set with past expiry).
///   2. Redirect to BlizzardOptions.AppBaseUrl.
/// </summary>
public class BattleNetLogoutFunction(
    IOptions<BlizzardOptions> blizzardOptions,
    IOptions<AuthOptions> authOptions,
    ILogger<BattleNetLogoutFunction> logger)
{
    private readonly BlizzardOptions _blizzardOpts = blizzardOptions.Value;
    private readonly AuthOptions _authOpts = authOptions.Value;

    [Function("battlenet-logout")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/logout")] HttpRequest req,
        FunctionContext ctx)
    {
        // Best-effort audit: the principal is present when a valid cookie reached us.
        var battleNetId = ctx.TryGetPrincipal()?.BattleNetId ?? "anonymous";
        AuditLog.Emit(logger, new AuditEvent("logout", battleNetId, null, "success", null));

        // Clear the auth cookie by deleting it. The attributes here must match the
        // ones used at set-time (see BattleNetCallbackFunction / E2ELoginFunction):
        // same Path + SameSite so the browser's cookie-store match succeeds.
        req.HttpContext.Response.Cookies.Delete(_authOpts.CookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = req.IsHttps,
            SameSite = SameSiteMode.Lax,
        });

        // Redirect to the home page.
        return new RedirectResult(_blizzardOpts.AppBaseUrl, permanent: false);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/logout</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-logout-v1")]
    public IActionResult RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/battlenet/logout")] HttpRequest req,
        FunctionContext ctx)
        => Run(req, ctx);
}
