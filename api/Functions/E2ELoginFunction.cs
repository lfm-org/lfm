// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

#if E2E
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Lfm.Api.Auth;
using Lfm.Api.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// Test-only endpoint that creates a session for a known E2E test user without
/// going through the Battle.net OAuth flow. Only registered when the
/// E2E_TEST_MODE environment variable is set to "true".
/// </summary>
public class E2ELoginFunction(ISessionCipher cipher, IOptions<AuthOptions> authOpts, IOptions<BlizzardOptions> blizzardOpts)
{
    [Function("e2e-login")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "e2e/login")] HttpRequest req)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("E2E_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return new NotFoundResult();
        }

        var auth = authOpts.Value;
        var blizzard = blizzardOpts.Value;

        var battleNetId = req.Query["battleNetId"].FirstOrDefault() ?? "test-bnet-id";

        // Known test identities: site-admin has no guild, others share the default guild.
        var (guildId, guildName) = battleNetId switch
        {
            "test-bnet-id-admin" => ((string?)null, (string?)null),
            _ => ("12345", (string?)"Test Guild"),
        };

        var principal = new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "TestUser#1234",
            GuildId: guildId,
            GuildName: guildName,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(auth.CookieMaxAgeHours));

        var encrypted = cipher.Protect(principal);

        req.HttpContext.Response.Cookies.Append(auth.CookieName, encrypted, new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // E2E runs on http://localhost
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = principal.ExpiresAt,
        });

        var redirect = req.Query["redirect"].FirstOrDefault() ?? blizzard.AppBaseUrl;
        if (!redirect.StartsWith("/") && !redirect.StartsWith(blizzard.AppBaseUrl))
        {
            redirect = blizzard.AppBaseUrl;
        }

        // If redirect is a relative path, prepend the app base URL
        if (redirect.StartsWith("/"))
        {
            redirect = blizzard.AppBaseUrl + redirect;
        }

        return new RedirectResult(redirect, permanent: false);
    }
}
#endif
