// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Auth;

public sealed record SessionPrincipal(
    string BattleNetId,
    string BattleTag,
    string? GuildId,
    string? GuildName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    // Battle.net OAuth access token — stored in the session cookie so that
    // endpoints like battlenet-characters-refresh (B2.5) can call the Blizzard
    // Profile API on behalf of the logged-in user without a separate token store.
    // Nullable for backward compatibility with sessions created before B2.5.
    string? AccessToken = null);
