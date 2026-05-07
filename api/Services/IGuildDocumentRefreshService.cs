// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

public interface IGuildDocumentRefreshService
{
    Task<GuildRefreshResult> RefreshForCurrentRaiderAsync(
        RaiderDocument raider,
        string? accessToken,
        GuildDocument? cached,
        CancellationToken ct);

    Task<GuildRefreshResult> RefreshExistingAsync(
        GuildDocument cached,
        string? accessToken,
        CancellationToken ct);

    Task<GuildRefreshResult> BootstrapForAdminAsync(
        string guildDocId,
        string? accessToken,
        IReadOnlyList<RaiderDocument> raiders,
        CancellationToken ct);
}

public sealed record GuildRefreshResult(
    GuildDocument? Guild,
    bool RefreshAttempted,
    bool UsedCachedFallback,
    GuildRefreshFailure? Failure);

public enum GuildRefreshFailure
{
    MissingGuildContext,
    MissingAccessToken,
    BlizzardUnavailable,
}
