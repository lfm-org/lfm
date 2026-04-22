// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Serialization;
using Lfm.Api.Services;
using Lfm.Contracts.Instances;
using Newtonsoft.Json;

namespace Lfm.Api.Repositories;

/// <summary>
/// Manifest entry emitted by the ingester (Phase 3) at
/// <c>reference/journal-instance/index.json</c>. When present, the reader returns
/// this directly — one blob GET for the whole list endpoint. When absent (first
/// deploy, pre-ingest), the reader falls back to enumerating per-id detail blobs.
/// </summary>
/// <summary>
/// Manifest entry at <c>reference/journal-instance/index.json</c>.
/// <para>
/// Category / ExpansionId / per-mode Difficulty+Size are nullable because
/// legacy manifests predating PR 3 of the create-run-page staging won't
/// carry them. The reader populates the corresponding <see cref="InstanceDto"/>
/// fields with sensible defaults when the manifest row is missing them; a
/// post-deploy <c>POST /api/wow/reference/refresh</c> re-hydrates every row.
/// </para>
/// </summary>
internal sealed record InstanceIndexEntry(
    int Id,
    string Name,
    IReadOnlyList<InstanceIndexMode>? Modes,
    string? Expansion,
    string? PortraitUrl,
    string? Category = null,
    int? ExpansionId = null);

/// <summary>
/// Per-mode manifest entry. <c>ModeKey</c> is the legacy composite
/// (<c>"{Difficulty}:{Size}"</c>) that pre-PR-3 manifests carried; the new
/// <c>Difficulty</c> + <c>Size</c> fields are populated by the current
/// ingester and preferred by new consumers. Both shapes round-trip so the
/// transition is cross-compatible.
/// </summary>
internal sealed record InstanceIndexMode(
    string ModeKey,
    string? Difficulty = null,
    int? Size = null);

/// <summary>
/// Verbatim Blizzard journal-instance detail as stored at
/// <c>reference/journal-instance/{id}.json</c>. Legacy TS-ingested blobs
/// carry localized-object names; the converter handles both shapes.
/// </summary>
internal sealed record JournalInstanceBlob(
    int Id,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    JournalInstanceExpansionBlob? Expansion = null,
    IReadOnlyList<JournalInstanceModeBlob>? Modes = null,
    JournalInstanceCategoryBlob? Category = null);

internal sealed record JournalInstanceExpansionBlob(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null,
    int? Id = null);

internal sealed record JournalInstanceCategoryBlob(string? Type = null);

internal sealed record JournalInstanceModeBlob(
    JournalModeRefBlob? Mode,
    int? Players);

internal sealed record JournalModeRefBlob(string Type);

internal sealed record MediaAssetBlob(string Key, string Value);

internal sealed record MediaAssetsBlob(IReadOnlyList<MediaAssetBlob>? Assets = null);

public sealed class InstancesRepository(IBlobReferenceClient blobs) : IInstancesRepository
{
    private const string Prefix = "reference/journal-instance/";
    private const string MediaPrefix = "reference/journal-instance-media/";
    private const string ManifestName = "reference/journal-instance/index.json";

    public async Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct)
    {
        // Fast path: single GET of the manifest produced by the ingester (Phase 3).
        var manifest = await blobs.GetAsync<List<InstanceIndexEntry>>(ManifestName, ct);
        if (manifest is not null)
            return ExpandManifest(manifest);

        // Fallback: enumerate per-id detail blobs + tolerantly fetch media for portraits.
        // Runs on the first deploy before the ingester has emitted a manifest.
        var rows = new List<InstanceDto>();
        await foreach (var detail in blobs.ListAsync<JournalInstanceBlob>(Prefix, ct))
        {
            var portraitUrl = await ResolvePortraitUrlAsync(detail.Id, ct);
            var expansion = detail.Expansion?.Name ?? "";
            var expansionId = detail.Expansion?.Id;
            var category = detail.Category?.Type;
            var instanceId = detail.Id.ToString();

            if (detail.Modes is null || detail.Modes.Count == 0)
            {
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:UNKNOWN:0",
                    InstanceNumericId: detail.Id,
                    Name: detail.Name,
                    ModeKey: "UNKNOWN:0",
                    Expansion: expansion,
                    Category: category,
                    ExpansionId: expansionId,
                    Difficulty: "UNKNOWN",
                    Size: 0,
                    PortraitUrl: portraitUrl));
                continue;
            }

            foreach (var mode in detail.Modes)
            {
                var modeType = mode.Mode?.Type ?? "UNKNOWN";
                var size = mode.Players ?? 0;
                var modeKey = $"{modeType}:{size}";
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:{modeKey}",
                    InstanceNumericId: detail.Id,
                    Name: detail.Name,
                    ModeKey: modeKey,
                    Expansion: expansion,
                    Category: category,
                    ExpansionId: expansionId,
                    Difficulty: modeType,
                    Size: size,
                    PortraitUrl: portraitUrl));
            }
        }
        return rows;
    }

    private async Task<string?> ResolvePortraitUrlAsync(int instanceId, CancellationToken ct)
    {
        var media = await blobs.GetAsync<MediaAssetsBlob>($"{MediaPrefix}{instanceId}.json", ct);
        if (media?.Assets is null) return null;

        // Blizzard journal-instance media returns assets keyed "tile" (newer) or
        // "image" (legacy). Prefer tile; fall back to image.
        foreach (var key in new[] { "tile", "image" })
        {
            foreach (var asset in media.Assets)
            {
                if (asset.Key == key && !string.IsNullOrEmpty(asset.Value))
                    return asset.Value;
            }
        }
        return null;
    }

    private static IReadOnlyList<InstanceDto> ExpandManifest(List<InstanceIndexEntry> manifest)
    {
        var rows = new List<InstanceDto>();
        foreach (var entry in manifest)
        {
            var expansion = entry.Expansion ?? "";
            var instanceId = entry.Id.ToString();

            if (entry.Modes is null || entry.Modes.Count == 0)
            {
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:UNKNOWN:0",
                    InstanceNumericId: entry.Id,
                    Name: entry.Name,
                    ModeKey: "UNKNOWN:0",
                    Expansion: expansion,
                    Category: entry.Category,
                    ExpansionId: entry.ExpansionId,
                    Difficulty: "UNKNOWN",
                    Size: 0,
                    PortraitUrl: entry.PortraitUrl));
                continue;
            }

            foreach (var mode in entry.Modes)
            {
                var (difficulty, size) = SplitModeKey(mode);
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:{mode.ModeKey}",
                    InstanceNumericId: entry.Id,
                    Name: entry.Name,
                    ModeKey: mode.ModeKey,
                    Expansion: expansion,
                    Category: entry.Category,
                    ExpansionId: entry.ExpansionId,
                    Difficulty: difficulty,
                    Size: size,
                    PortraitUrl: entry.PortraitUrl));
            }
        }
        return rows;
    }

    /// <summary>
    /// Prefer the new structured <see cref="InstanceIndexMode.Difficulty"/> +
    /// <see cref="InstanceIndexMode.Size"/> fields the ingester now writes;
    /// fall back to splitting the legacy composite <c>ModeKey</c> when reading
    /// a pre-PR-3 manifest. Round-trippable — the ingester writes both so a
    /// re-ingest converges the manifest onto the new shape.
    /// </summary>
    private static (string Difficulty, int Size) SplitModeKey(InstanceIndexMode mode)
    {
        if (!string.IsNullOrEmpty(mode.Difficulty) && mode.Size.HasValue)
            return (mode.Difficulty, mode.Size.Value);

        var parts = mode.ModeKey.Split(':', 2);
        var difficulty = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "UNKNOWN";
        var size = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        return (difficulty, size);
    }
}
