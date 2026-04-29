// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Lfm.App.i18n;
using Lfm.App.Services;

namespace Lfm.App.Auth;

/// <summary>
/// Blazor authentication state provider that derives identity from GET /api/me.
/// The result is cached for the lifetime of the scoped instance (one browser session).
/// Call <see cref="NotifyStateChanged"/> to force a re-check (e.g. after login/logout).
/// </summary>
public sealed class AppAuthenticationStateProvider(IMeClient meClient, ILocaleService localeService)
    : AuthenticationStateProvider
{
    private AuthenticationState? _cached;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null)
            return _cached;

        var me = await meClient.GetAsync(CancellationToken.None);
        if (me is null)
        {
            // Do not cache the anonymous result — a transient network failure
            // or cold-start race must not cement the user as anonymous for the
            // rest of the session. Let the next call retry MeClient.
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Apply the user's persisted locale preference (overrides browser detection).
        if (!string.IsNullOrEmpty(me.Locale))
            localeService.SetLocale(me.Locale);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, me.BattleNetId),
            new(ClaimTypes.Name, me.BattleNetId),
        };

        if (!string.IsNullOrEmpty(me.GuildName))
            claims.Add(new Claim("guild_name", me.GuildName));

        if (me.IsSiteAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "SiteAdmin"));

        var identity = new ClaimsIdentity(claims, "BattleNet");
        _cached = new AuthenticationState(new ClaimsPrincipal(identity));
        return _cached;
    }

    public void NotifyStateChanged()
    {
        _cached = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
