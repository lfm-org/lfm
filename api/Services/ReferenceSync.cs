// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Contracts.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Syncs Blizzard reference data (journal-instance, journal-expansion,
/// playable-specialization, Mythic Keystone leaderboard membership, plus media blobs)
/// from the Blizzard Game Data API into the
/// <c>lfmstore/wow/reference/</c> blob container — see
/// <c>docs/storage-architecture.md</c>.
///
/// For each entity the sync:
/// 1. Fetches the Blizzard index and iterates entries.
/// 2. Per entry: fetches the detail (with simple 429 retry) + media (best-effort).
/// 3. Writes per-id detail + media blobs.
/// 4. At the end, writes <c>reference/{kind}/index.json</c> — a self-contained
///    list-endpoint manifest that the repositories read as a single blob GET.
///
/// Per-entity failures are caught by <see cref="SyncAllAsync"/>; the remaining
/// entities are still attempted.
/// </summary>
public sealed class ReferenceSync(
    IBlizzardGameDataClient gameData,
    IBlobReferenceClient blobs,
    IOptions<BlizzardOptions> blizzardOptions,
    ILogger<ReferenceSync> logger) : IReferenceSync
{
    private const int CurrentRaidTierJournalExpansionId = 516;
    private static readonly IReadOnlyDictionary<string, int> DefaultLeaderboardConnectedRealms =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["eu"] = 1080,
            ["us"] = 11,
        };

    /// <inheritdoc/>
    public async Task<WowReferenceRefreshResponse> SyncAllAsync(
        CancellationToken ct,
        IProgress<WowReferenceRefreshProgress>? progress = null)
    {
        var results = new List<WowReferenceRefreshEntityResult>();
        string? token = null;

        await RunEntityAsync(
            entity: "instances",
            results: results,
            run: async () =>
            {
                token ??= await gameData.GetClientCredentialsTokenAsync(ct);
                var count = await SyncInstancesAsync(token, progress, ct);
                return $"synced ({count} docs)";
            },
            progress: progress,
            ct: ct);

        await RunEntityAsync(
            entity: "specializations",
            results: results,
            run: async () =>
            {
                token ??= await gameData.GetClientCredentialsTokenAsync(ct);
                var count = await SyncSpecializationsAsync(token, progress, ct);
                return $"synced ({count} docs)";
            },
            progress: progress,
            ct: ct);

        await RunEntityAsync(
            entity: "expansions",
            results: results,
            run: async () =>
            {
                token ??= await gameData.GetClientCredentialsTokenAsync(ct);
                var count = await SyncExpansionsAsync(token, progress, ct);
                return $"synced ({count} docs)";
            },
            progress: progress,
            ct: ct);

        return new WowReferenceRefreshResponse(results);
    }

    private async Task RunEntityAsync(
        string entity,
        List<WowReferenceRefreshEntityResult> results,
        Func<Task<string>> run,
        IProgress<WowReferenceRefreshProgress>? progress,
        CancellationToken ct)
    {
        string status;
        try
        {
            status = await run();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync {Entity}", entity);
            status = $"failed: {ex.Message}";
        }
        results.Add(new WowReferenceRefreshEntityResult(entity, status));
        progress?.Report(new WowReferenceRefreshProgress(
            Entity: entity, Phase: "end", Processed: 0, Total: 0, Current: null, Status: status));
    }

    // ---------------------------------------------------------------------------
    // Instance sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncInstancesAsync(
        string token,
        IProgress<WowReferenceRefreshProgress>? progress,
        CancellationToken ct)
    {
        var expansions = await gameData.GetJournalExpansionIndexAsync(token, ct);
        var currentRaidTierIds = await ResolveCurrentRaidTierRaidIdsAsync(token, ct);
        var currentKeystoneIds = await ResolveCurrentMythicKeystoneDungeonIdsAsync(token, ct);
        var hasMembershipFilter = currentRaidTierIds.Count > 0 || currentKeystoneIds.Count > 0;
        var index = await gameData.GetJournalInstanceIndexAsync(token, ct);
        var manifest = new List<InstanceIndexEntry>();
        var total = index.Instances.Count;
        var processed = 0;
        progress?.Report(new WowReferenceRefreshProgress(
            "instances", "start", Processed: 0, Total: total));

        foreach (var entry in index.Instances)
        {
            if (hasMembershipFilter
                && !currentRaidTierIds.Contains(entry.Id)
                && !currentKeystoneIds.Contains(entry.Id))
            {
                continue;
            }

            processed++;
            progress?.Report(new WowReferenceRefreshProgress(
                "instances", "progress", processed, total, Current: entry.Name));
            var detail = await FetchWithRetryAsync(
                () => gameData.GetJournalInstanceAsync(entry.Id, token, ct),
                $"instance {entry.Id}",
                ct);
            if (detail is null) continue;

            BlizzardMediaAssets? media = null;
            try
            {
                media = await gameData.GetJournalInstanceMediaAsync(entry.Id, token, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not fetch media for instance {Id}", entry.Id);
            }

            var modeBlobs = detail.Modes?
                .Select(m => new JournalInstanceModeBlob(new JournalModeRefBlob(m.Mode.Type), m.Players))
                .ToList();
            var detailBlob = new JournalInstanceBlob(
                Id: detail.Id,
                Name: detail.Name,
                Expansion: detail.Expansion is null
                    ? null
                    : new JournalInstanceExpansionBlob(detail.Expansion.Name, detail.Expansion.Id),
                Modes: modeBlobs,
                Category: detail.Category is null ? null : new JournalInstanceCategoryBlob(detail.Category.Type));
            await blobs.UploadAsync($"reference/journal-instance/{detail.Id}.json", detailBlob, ct);

            string? portraitUrl = null;
            if (media?.Assets is not null)
            {
                var assetBlobs = media.Assets.Select(a => new MediaAssetBlob(a.Key, a.Value)).ToList();
                await blobs.UploadAsync(
                    $"reference/journal-instance-media/{detail.Id}.json",
                    new MediaAssetsBlob(assetBlobs),
                    ct);
                portraitUrl = assetBlobs.FirstOrDefault(a => a.Key == "tile")?.Value
                           ?? assetBlobs.FirstOrDefault(a => a.Key == "image")?.Value;
            }

            // Write both the legacy composite ModeKey and the new structured
            // Difficulty + Size on each manifest mode row. New consumers read
            // Difficulty / Size; legacy readers continue to parse ModeKey.
            var manifestModes = (detail.Modes?.Count ?? 0) == 0
                ? new List<InstanceIndexMode> { new("UNKNOWN:0", "UNKNOWN", 0) }
                : detail.Modes!
                    .Select(m =>
                    {
                        var size = m.Players ?? 0;
                        return new InstanceIndexMode($"{m.Mode.Type}:{size}", m.Mode.Type, size);
                    })
                    .ToList();

            manifest.Add(new InstanceIndexEntry(
                Id: detail.Id,
                Name: detail.Name,
                Modes: manifestModes,
                Expansion: detail.Expansion?.Name ?? "",
                PortraitUrl: portraitUrl,
                Category: detail.Category?.Type,
                ExpansionId: detail.Expansion?.Id,
                IsCurrentMythicKeystone: currentKeystoneIds.Contains(detail.Id)));
        }

        await blobs.UploadAsync("reference/journal-instance/index.json", manifest, ct);
        return manifest.Count;
    }

    private async Task<HashSet<int>> ResolveCurrentRaidTierRaidIdsAsync(
        string token,
        CancellationToken ct)
    {
        var detail = await FetchWithRetryAsync(
            () => gameData.GetJournalExpansionAsync(CurrentRaidTierJournalExpansionId, token, ct),
            $"journal expansion {CurrentRaidTierJournalExpansionId}",
            ct);

        return detail?.Raids is null
            ? []
            : detail.Raids.Select(r => r.Id).ToHashSet();
    }

    private async Task<HashSet<int>> ResolveCurrentMythicKeystoneDungeonIdsAsync(
        string token,
        CancellationToken ct)
    {
        try
        {
            var index = await gameData.GetMythicKeystoneSeasonIndexAsync(token, ct);
            if (index.CurrentSeason is null) return [];

            return await ResolveCurrentMythicKeystoneDungeonIdsFromLeaderboardsAsync(token, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve current Mythic Keystone leaderboard dungeons");
            return [];
        }
    }

    private async Task<HashSet<int>> ResolveCurrentMythicKeystoneDungeonIdsFromLeaderboardsAsync(
        string token,
        CancellationToken ct)
    {
        try
        {
            var defaultConnectedRealmId = ResolveDefaultLeaderboardConnectedRealmId();
            if (defaultConnectedRealmId is int id)
            {
                var leaderboards = await FetchWithRetryAsync(
                    () => gameData.GetMythicKeystoneLeaderboardsIndexAsync(id, token, ct),
                    $"mythic keystone leaderboard index {id}",
                    ct);

                if (leaderboards?.CurrentLeaderboards is { Count: > 0 })
                    return leaderboards.CurrentLeaderboards.Select(d => d.Id).ToHashSet();
            }

            var connectedRealms = await FetchWithRetryAsync(
                () => gameData.GetConnectedRealmIndexAsync(token, ct),
                "connected realm index",
                ct);
            if (connectedRealms?.ConnectedRealms is null) return [];

            foreach (var connectedRealmId in connectedRealms.ConnectedRealms
                         .Select(r => TryGetConnectedRealmId(r.Key.Href))
                         .Where(id => id.HasValue)
                         .Select(id => id!.Value))
            {
                var leaderboards = await FetchWithRetryAsync(
                    () => gameData.GetMythicKeystoneLeaderboardsIndexAsync(connectedRealmId, token, ct),
                    $"mythic keystone leaderboard index {connectedRealmId}",
                    ct);

                if (leaderboards?.CurrentLeaderboards is { Count: > 0 })
                    return leaderboards.CurrentLeaderboards.Select(d => d.Id).ToHashSet();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve current Mythic Keystone dungeons from leaderboard index");
        }

        return [];
    }

    private int? ResolveDefaultLeaderboardConnectedRealmId()
    {
        var region = blizzardOptions.Value.Region.ToLowerInvariant();
        return DefaultLeaderboardConnectedRealms.TryGetValue(region, out var connectedRealmId)
            ? connectedRealmId
            : null;
    }

    private static int? TryGetConnectedRealmId(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return null;

        var lastSegment = uri.Segments
            .Select(s => s.Trim('/'))
            .LastOrDefault(s => s.Length > 0);

        return int.TryParse(lastSegment, out var id) ? id : null;
    }

    // ---------------------------------------------------------------------------
    // Specialization sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncSpecializationsAsync(
        string token,
        IProgress<WowReferenceRefreshProgress>? progress,
        CancellationToken ct)
    {
        var index = await gameData.GetPlayableSpecIndexAsync(token, ct);
        var manifest = new List<SpecializationIndexEntry>();
        var total = index.CharacterSpecializations.Count;
        var processed = 0;
        progress?.Report(new WowReferenceRefreshProgress(
            "specializations", "start", Processed: 0, Total: total));

        foreach (var entry in index.CharacterSpecializations)
        {
            processed++;
            progress?.Report(new WowReferenceRefreshProgress(
                "specializations", "progress", processed, total, Current: entry.Name));
            var detail = await FetchWithRetryAsync(
                () => gameData.GetPlayableSpecAsync(entry.Id, token, ct),
                $"specialization {entry.Id}",
                ct);
            if (detail is null) continue;

            BlizzardMediaAssets? media = null;
            try
            {
                media = await gameData.GetPlayableSpecMediaAsync(entry.Id, token, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not fetch media for spec {Id}", entry.Id);
            }

            var detailBlob = new PlayableSpecializationBlob(
                Id: detail.Id,
                Name: detail.Name,
                PlayableClass: new PlayableClassRefBlob(detail.PlayableClass.Id),
                Role: new PlayableSpecRoleRefBlob(detail.Role.Type));
            await blobs.UploadAsync($"reference/playable-specialization/{detail.Id}.json", detailBlob, ct);

            string? iconUrl = null;
            if (media?.Assets is not null)
            {
                var assetBlobs = media.Assets.Select(a => new MediaAssetBlob(a.Key, a.Value)).ToList();
                await blobs.UploadAsync(
                    $"reference/playable-specialization-media/{detail.Id}.json",
                    new MediaAssetsBlob(assetBlobs),
                    ct);
                iconUrl = assetBlobs.FirstOrDefault(a => a.Key == "icon")?.Value;
            }

            manifest.Add(new SpecializationIndexEntry(
                Id: detail.Id,
                Name: detail.Name,
                ClassId: detail.PlayableClass.Id,
                Role: ToRole(detail.Role.Type),
                IconUrl: iconUrl));
        }

        await blobs.UploadAsync("reference/playable-specialization/index.json", manifest, ct);
        return manifest.Count;
    }

    // ---------------------------------------------------------------------------
    // Expansion sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncExpansionsAsync(
        string token,
        IProgress<WowReferenceRefreshProgress>? progress,
        CancellationToken ct)
    {
        var index = await gameData.GetJournalExpansionIndexAsync(token, ct);
        var total = index.Tiers.Count;
        progress?.Report(new WowReferenceRefreshProgress(
            "expansions", "start", Processed: 0, Total: total));

        var manifest = new List<ExpansionIndexEntry>(total);
        var processed = 0;
        foreach (var t in index.Tiers)
        {
            processed++;
            progress?.Report(new WowReferenceRefreshProgress(
                "expansions", "progress", processed, total, Current: t.Name));
            manifest.Add(new ExpansionIndexEntry(Id: t.Id, Name: t.Name));
        }

        await blobs.UploadAsync("reference/journal-expansion/index.json", manifest, ct);
        return manifest.Count;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Simple 429-retry wrapper: sleep 1s and retry on <c>HttpStatusCode.TooManyRequests</c>
    /// up to <see cref="MaxRetries429"/> times, log + skip on any other error or once
    /// the cap is exceeded. The shared <c>BlizzardRateLimiter</c> gates outbound
    /// traffic to ~80 req/s so genuine 429s from Blizzard are rare; the cap
    /// prevents a sustained outage from burning the entire Functions invocation
    /// timeout in a tight retry loop.
    /// </summary>
    private const int MaxRetries429 = 5;
    private static readonly TimeSpan Retry429Delay = TimeSpan.FromSeconds(1);

    private Task<T?> FetchWithRetryAsync<T>(
        Func<Task<T>> fetch,
        string description,
        CancellationToken ct) where T : class
        => FetchWithRetryAsyncCore(fetch, description, MaxRetries429, Retry429Delay, logger, ct);

    // Test-only seam — exposed via InternalsVisibleTo. Keeps production callers unchanged.
    internal static Task<T?> FetchWithRetryAsyncForTests<T>(
        Func<Task<T>> fetch,
        string description,
        int maxRetries,
        TimeSpan retryDelay,
        ILogger logger,
        CancellationToken ct) where T : class
        => FetchWithRetryAsyncCore(fetch, description, maxRetries, retryDelay, logger, ct);

    private static async Task<T?> FetchWithRetryAsyncCore<T>(
        Func<Task<T>> fetch,
        string description,
        int maxRetries,
        TimeSpan retryDelay,
        ILogger logger,
        CancellationToken ct) where T : class
    {
        var attempt = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await fetch();
            }
            catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
            {
                if (attempt++ >= maxRetries)
                {
                    logger.LogWarning(
                        "Skipping {Description}: 429 retry cap exceeded ({Cap} retries)",
                        description, maxRetries);
                    return null;
                }
                await Task.Delay(retryDelay, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping {Description}: fetch failed", description);
                return null;
            }
        }
    }

    private static string ToRole(string blizzardRoleType) => blizzardRoleType switch
    {
        "HEALER" => "HEALER",
        "TANK" => "TANK",
        _ => "DPS",
    };
}
