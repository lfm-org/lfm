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
internal sealed record InstanceIndexEntry(
    int Id,
    string Name,
    IReadOnlyList<InstanceIndexMode>? Modes,
    string? Expansion,
    string? PortraitUrl);

internal sealed record InstanceIndexMode(string ModeKey);

/// <summary>
/// Verbatim Blizzard journal-instance detail as stored at
/// <c>reference/journal-instance/{id}.json</c>. Legacy TS-ingested blobs
/// carry localized-object names; the converter handles both shapes.
/// </summary>
internal sealed record JournalInstanceBlob(
    int Id,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    JournalInstanceExpansionBlob? Expansion = null,
    IReadOnlyList<JournalInstanceModeBlob>? Modes = null);

internal sealed record JournalInstanceExpansionBlob(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

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
            var instanceId = detail.Id.ToString();

            if (detail.Modes is null || detail.Modes.Count == 0)
            {
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:UNKNOWN:0",
                    InstanceNumericId: detail.Id,
                    Name: detail.Name,
                    ModeKey: "UNKNOWN:0",
                    Expansion: expansion,
                    PortraitUrl: portraitUrl));
                continue;
            }

            foreach (var mode in detail.Modes)
            {
                var modeType = mode.Mode?.Type ?? "UNKNOWN";
                var modeKey = $"{modeType}:{mode.Players ?? 0}";
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:{modeKey}",
                    InstanceNumericId: detail.Id,
                    Name: detail.Name,
                    ModeKey: modeKey,
                    Expansion: expansion,
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
                    PortraitUrl: entry.PortraitUrl));
                continue;
            }

            foreach (var mode in entry.Modes)
            {
                rows.Add(new InstanceDto(
                    Id: $"{instanceId}:{mode.ModeKey}",
                    InstanceNumericId: entry.Id,
                    Name: entry.Name,
                    ModeKey: mode.ModeKey,
                    Expansion: expansion,
                    PortraitUrl: entry.PortraitUrl));
            }
        }
        return rows;
    }
}
