// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Admin;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Services;

/// <summary>
/// Syncs static Blizzard reference data (journal-instance, playable-specialization,
/// plus their media blobs) from the Blizzard Game Data API into the
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
    ILogger<ReferenceSync> logger) : IReferenceSync
{
    /// <inheritdoc/>
    public async Task<WowReferenceRefreshResponse> SyncAllAsync(CancellationToken ct)
    {
        var results = new List<WowReferenceRefreshEntityResult>();
        string? token = null;

        try
        {
            token ??= await gameData.GetClientCredentialsTokenAsync(ct);
            var count = await SyncInstancesAsync(token, ct);
            results.Add(new WowReferenceRefreshEntityResult("instances", $"synced ({count} docs)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync instances");
            results.Add(new WowReferenceRefreshEntityResult("instances", $"failed: {ex.Message}"));
        }

        try
        {
            token ??= await gameData.GetClientCredentialsTokenAsync(ct);
            var count = await SyncSpecializationsAsync(token, ct);
            results.Add(new WowReferenceRefreshEntityResult("specializations", $"synced ({count} docs)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync specializations");
            results.Add(new WowReferenceRefreshEntityResult("specializations", $"failed: {ex.Message}"));
        }

        try
        {
            token ??= await gameData.GetClientCredentialsTokenAsync(ct);
            var count = await SyncExpansionsAsync(token, ct);
            results.Add(new WowReferenceRefreshEntityResult("expansions", $"synced ({count} docs)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync expansions");
            results.Add(new WowReferenceRefreshEntityResult("expansions", $"failed: {ex.Message}"));
        }

        return new WowReferenceRefreshResponse(results);
    }

    // ---------------------------------------------------------------------------
    // Instance sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncInstancesAsync(string token, CancellationToken ct)
    {
        var index = await gameData.GetJournalInstanceIndexAsync(token, ct);
        var manifest = new List<InstanceIndexEntry>();

        foreach (var entry in index.Instances)
        {
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
                ExpansionId: detail.Expansion?.Id));
        }

        await blobs.UploadAsync("reference/journal-instance/index.json", manifest, ct);
        return manifest.Count;
    }

    // ---------------------------------------------------------------------------
    // Specialization sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncSpecializationsAsync(string token, CancellationToken ct)
    {
        var index = await gameData.GetPlayableSpecIndexAsync(token, ct);
        var manifest = new List<SpecializationIndexEntry>();

        foreach (var entry in index.CharacterSpecializations)
        {
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

    private async Task<int> SyncExpansionsAsync(string token, CancellationToken ct)
    {
        var index = await gameData.GetJournalExpansionIndexAsync(token, ct);
        var manifest = index.Tiers
            .Select(t => new ExpansionIndexEntry(Id: t.Id, Name: t.Name))
            .ToList();

        await blobs.UploadAsync("reference/journal-expansion/index.json", manifest, ct);
        return manifest.Count;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Simple 429-retry wrapper: sleep 1s and retry on <c>HttpStatusCode.TooManyRequests</c>,
    /// log + skip on any other error. The shared <c>BlizzardRateLimiter</c> gates
    /// outbound traffic to ~80 req/s so genuine 429s from Blizzard are rare, but
    /// the retry is kept for safety.
    /// </summary>
    private async Task<T?> FetchWithRetryAsync<T>(
        Func<Task<T>> fetch,
        string description,
        CancellationToken ct) where T : class
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await fetch();
            }
            catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
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
