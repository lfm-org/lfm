// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Lfm.Api.Repositories;
using Lfm.Api.Services.Blizzard;

namespace Lfm.Api.Services;

public sealed partial class GuildDocumentRefreshService(
    IGuildRepository guildRepo,
    IBlizzardProfileClient blizzardClient) : IGuildDocumentRefreshService
{
    public async Task<GuildRefreshResult> RefreshForCurrentRaiderAsync(
        RaiderDocument raider,
        string? accessToken,
        GuildDocument? cached,
        CancellationToken ct)
    {
        if (cached is not null && GuildRosterMatcher.IsFresh(cached.BlizzardRosterFetchedAt))
            return new GuildRefreshResult(cached, RefreshAttempted: false, UsedCachedFallback: false, Failure: null);

        var context = ResolveSelectedGuildContext(raider);
        if (context is null)
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: false,
                UsedCachedFallback: cached is not null,
                GuildRefreshFailure.MissingGuildContext);
        }

        return await RefreshAsync(
            context.GuildDocId,
            context.GuildId,
            context.GuildName,
            context.RealmSlug,
            accessToken,
            cached,
            ct);
    }

    public async Task<GuildRefreshResult> RefreshExistingAsync(
        GuildDocument cached,
        string? accessToken,
        CancellationToken ct)
    {
        if (GuildRosterMatcher.IsFresh(cached.BlizzardRosterFetchedAt))
            return new GuildRefreshResult(cached, RefreshAttempted: false, UsedCachedFallback: false, Failure: null);

        var guildName = cached.BlizzardProfileRaw?.Name;
        if (string.IsNullOrWhiteSpace(guildName) || string.IsNullOrWhiteSpace(cached.RealmSlug))
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: false,
                UsedCachedFallback: true,
                GuildRefreshFailure.MissingGuildContext);
        }

        return await RefreshAsync(
            cached.Id,
            cached.GuildId,
            guildName,
            cached.RealmSlug,
            accessToken,
            cached,
            ct);
    }

    public Task<GuildRefreshResult> BootstrapForAdminAsync(
        string guildDocId,
        string? accessToken,
        IReadOnlyList<RaiderDocument> raiders,
        CancellationToken ct)
    {
        if (!int.TryParse(guildDocId, NumberStyles.None, CultureInfo.InvariantCulture, out var guildId))
        {
            return Task.FromResult(new GuildRefreshResult(
                Guild: null,
                RefreshAttempted: false,
                UsedCachedFallback: false,
                GuildRefreshFailure.MissingGuildContext));
        }

        var context = ResolveGuildContextFromRaiders(guildId, raiders);
        if (context is null)
        {
            return Task.FromResult(new GuildRefreshResult(
                Guild: null,
                RefreshAttempted: false,
                UsedCachedFallback: false,
                GuildRefreshFailure.MissingGuildContext));
        }

        return RefreshAsync(
            guildDocId,
            guildId,
            context.GuildName,
            context.RealmSlug,
            accessToken,
            cached: null,
            ct);
    }

    private async Task<GuildRefreshResult> RefreshAsync(
        string guildDocId,
        int guildId,
        string guildName,
        string realmSlug,
        string? accessToken,
        GuildDocument? cached,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: false,
                UsedCachedFallback: cached is not null,
                GuildRefreshFailure.MissingAccessToken);
        }

        var guildNameSlug = ToGuildNameSlug(guildName);
        if (string.IsNullOrWhiteSpace(guildNameSlug))
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: false,
                UsedCachedFallback: cached is not null,
                GuildRefreshFailure.MissingGuildContext);
        }

        try
        {
            var profileTask = blizzardClient.GetGuildProfileAsync(realmSlug, guildNameSlug, accessToken, ct);
            var rosterTask = blizzardClient.GetGuildRosterAsync(realmSlug, guildNameSlug, accessToken, ct);
            await Task.WhenAll(profileTask, rosterTask);

            var fetchedAt = DateTimeOffset.UtcNow.ToString("O");
            var updated = new GuildDocument(
                Id: guildDocId,
                GuildId: guildId,
                RealmSlug: realmSlug,
                Slogan: cached?.Slogan,
                BlizzardRosterFetchedAt: fetchedAt,
                BlizzardProfileFetchedAt: fetchedAt,
                CrestEmblemUrl: cached?.CrestEmblemUrl,
                CrestBorderUrl: cached?.CrestBorderUrl,
                RankPermissions: cached?.RankPermissions,
                Setup: cached?.Setup,
                LastOverrideBy: cached?.LastOverrideBy,
                LastOverrideAt: cached?.LastOverrideAt,
                BlizzardRosterRaw: BlizzardModelTranslator.ToStored(rosterTask.Result),
                BlizzardProfileRaw: BlizzardModelTranslator.ToStored(profileTask.Result));

            var persisted = await guildRepo.UpsertAsync(updated, ct);
            return new GuildRefreshResult(persisted, RefreshAttempted: true, UsedCachedFallback: false, Failure: null);
        }
        catch (HttpRequestException)
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: true,
                UsedCachedFallback: cached is not null,
                GuildRefreshFailure.BlizzardUnavailable);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new GuildRefreshResult(
                cached,
                RefreshAttempted: true,
                UsedCachedFallback: cached is not null,
                GuildRefreshFailure.BlizzardUnavailable);
        }
    }

    private static GuildRefreshContext? ResolveSelectedGuildContext(RaiderDocument raider)
    {
        if (raider.SelectedCharacterId is null || raider.Characters is null)
            return null;

        var selected = raider.Characters.FirstOrDefault(c => c.Id == raider.SelectedCharacterId);
        if (selected?.GuildId is null ||
            string.IsNullOrWhiteSpace(selected.GuildName) ||
            string.IsNullOrWhiteSpace(selected.Realm))
        {
            return null;
        }

        return new GuildRefreshContext(
            selected.GuildId.Value.ToString(CultureInfo.InvariantCulture),
            selected.GuildId.Value,
            selected.GuildName,
            selected.Realm);
    }

    private static GuildRefreshContext? ResolveGuildContextFromRaiders(
        int guildId,
        IReadOnlyList<RaiderDocument> raiders)
    {
        foreach (var character in raiders.SelectMany(r => r.Characters ?? Array.Empty<StoredSelectedCharacter>()))
        {
            if (character.GuildId == guildId &&
                !string.IsNullOrWhiteSpace(character.GuildName) &&
                !string.IsNullOrWhiteSpace(character.Realm))
            {
                return new GuildRefreshContext(
                    guildId.ToString(CultureInfo.InvariantCulture),
                    guildId,
                    character.GuildName,
                    character.Realm);
            }
        }

        return null;
    }

    private static string ToGuildNameSlug(string guildName)
        => NonSlugCharacters().Replace(
            Whitespace().Replace(guildName.Trim().ToLowerInvariant(), "-"),
            string.Empty);

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex NonSlugCharacters();

    private sealed record GuildRefreshContext(
        string GuildDocId,
        int GuildId,
        string GuildName,
        string RealmSlug);
}
