// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// GET /api/battlenet/callback — OAuth callback from Battle.net. Completes the
/// PKCE authorization-code flow and establishes an encrypted session cookie.
///
/// Happy path:
///   1. Read login_state cookie and state query parameter.
///   2. Unprotect login_state → {state, codeVerifier, redirect}. Validate state matches.
///   3. Exchange code + codeVerifier for an access token.
///   4. Fetch Battle.net user info (id + battletag).
///   5. Upsert raider document (create on first login, touch lastSeenAt on return).
///   6. Create SessionPrincipal and encrypt via ISessionCipher.
///   7. Set auth cookie, clear login_state cookie, redirect to AppBaseUrl + redirect.
///
/// Error paths (all redirect to {AppBaseUrl}/auth/failure to avoid leaking details):
///   - Missing or mismatched state / login_state cookie.
///   - Failed code exchange (Battle.net returned an error).
/// </summary>
public class BattleNetCallbackFunction(
    IBlizzardOAuthClient oauthClient,
    ISessionCipher sessionCipher,
    IRaidersRepository raiders,
    IOptions<BlizzardOptions> blizzardOptions,
    IOptions<AuthOptions> authOptions,
    ILogger<BattleNetCallbackFunction> logger)
{
    private readonly BlizzardOptions _blizzardOpts = blizzardOptions.Value;
    private readonly AuthOptions _authOpts = authOptions.Value;

    [Function("battlenet-callback")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/callback")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var code = req.Query["code"].FirstOrDefault();
        var urlState = req.Query["state"].FirstOrDefault();

        // Read the login_state cookie set by the login handler.
        var loginStateCookieRaw = req.Cookies["login_state"];

        // Both cookie and state query param are required.
        if (string.IsNullOrEmpty(loginStateCookieRaw) || string.IsNullOrEmpty(urlState))
            return RejectWithClearedCookie(req, "missing login_state or state");

        // Unprotect and validate the login state cookie.
        var loginState = oauthClient.UnprotectLoginState(loginStateCookieRaw);
        if (loginState is null || loginState.Value.state != urlState)
            return RejectWithClearedCookie(req, "state mismatch");

        var codeVerifier = loginState.Value.codeVerifier;
        var postLoginRedirect = loginState.Value.redirect;

        // Exchange the authorization code for an access token.
        BlizzardTokenResponse token;
        try
        {
            if (string.IsNullOrEmpty(code))
                return RejectWithClearedCookie(req, "missing authorization code");
            token = await oauthClient.ExchangeCodeAsync(code, codeVerifier, cancellationToken);
        }
        catch
        {
            return RejectWithClearedCookie(req, "token exchange failed");
        }

        // Fetch user identity from Battle.net.
        BlizzardUserInfo userInfo;
        try
        {
            userInfo = await oauthClient.GetUserInfoAsync(token.AccessToken, cancellationToken);
        }
        catch
        {
            return RejectWithClearedCookie(req, "user info fetch failed");
        }

        var battleNetId = userInfo.Id.ToString();

        // Upsert the raider document. LastSeenAt must refresh on every login
        // because the cleanup timer deletes missing or stale raider documents.
        var existing = await raiders.GetByBattleNetIdAsync(battleNetId, cancellationToken);
        var nowIso = DateTimeOffset.UtcNow.ToString("o");
        var raider = existing is not null
            ? existing with { LastSeenAt = nowIso, Ttl = 180 * 86400 }
            : new RaiderDocument(
                Id: battleNetId,
                BattleNetId: battleNetId,
                SelectedCharacterId: null,
                Locale: null,
                LastSeenAt: nowIso,
                Ttl: 180 * 86400);
        await raiders.UpsertAsync(raider, cancellationToken);

        // Create the session principal. Guild info is not available at login time;
        // it is populated later when the user selects a character (me-update flow).
        var now = DateTimeOffset.UtcNow;
        var principal = new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: userInfo.BattleTag,
            GuildId: null,
            GuildName: null,
            IssuedAt: now,
            ExpiresAt: now.AddHours(_authOpts.CookieMaxAgeHours),
            AccessToken: token.AccessToken);

        var encryptedToken = sessionCipher.Protect(principal);

        // Set the auth cookie.
        req.HttpContext.Response.Cookies.Append(_authOpts.CookieName, encryptedToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = principal.ExpiresAt,
        });

        AuditLog.Emit(logger, new AuditEvent("login.success", battleNetId, null, "success", null));

        // Clear the login_state cookie.
        ClearLoginStateCookie(req);

        // Redirect to the originally-requested page (if any) or fall back to AppBaseUrl.
        var destination = !string.IsNullOrEmpty(postLoginRedirect)
            ? $"{_blizzardOpts.AppBaseUrl}{postLoginRedirect}"
            : _blizzardOpts.AppBaseUrl;
        return new RedirectResult(destination, permanent: false);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/callback</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-callback-v1")]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/battlenet/callback")] HttpRequest req,
        CancellationToken cancellationToken)
        => Run(req, cancellationToken);

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private IActionResult RejectWithClearedCookie(HttpRequest req, string detail)
    {
        AuditLog.Emit(logger, new AuditEvent("login.failure", "unknown", null, "failure", detail));
        ClearLoginStateCookie(req);
        return new RedirectResult($"{_blizzardOpts.AppBaseUrl}/auth/failure", permanent: false);
    }

    private static void ClearLoginStateCookie(HttpRequest req)
    {
        req.HttpContext.Response.Cookies.Append("login_state", string.Empty, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.Zero,
        });
    }
}
