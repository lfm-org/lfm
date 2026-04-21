// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Serialization;
using Lfm.Api.Services;
using Lfm.Contracts.Specializations;
using Newtonsoft.Json;

namespace Lfm.Api.Repositories;

/// <summary>
/// Manifest entry emitted by the ingester (Phase 3) at
/// <c>reference/playable-specialization/index.json</c>. When present, the reader
/// returns this directly — one blob GET for the whole list endpoint. When absent
/// (first deploy, pre-ingest), the reader falls back to enumerating per-id
/// detail blobs plus per-id media blobs for icon URLs.
/// </summary>
internal sealed record SpecializationIndexEntry(
    int Id,
    string Name,
    int ClassId,
    string Role,
    string? IconUrl);

/// <summary>
/// Verbatim Blizzard playable-specialization detail as stored at
/// <c>reference/playable-specialization/{id}.json</c>. Legacy TS-ingested blobs
/// carry localized-object names; the converter handles both shapes.
/// </summary>
internal sealed record PlayableSpecializationBlob(
    int Id,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    [property: JsonProperty("playable_class")] PlayableClassRefBlob? PlayableClass = null,
    PlayableSpecRoleRefBlob? Role = null);

internal sealed record PlayableClassRefBlob(int Id);

internal sealed record PlayableSpecRoleRefBlob(string Type);

public sealed class SpecializationsRepository(IBlobReferenceClient blobs) : ISpecializationsRepository
{
    private const string Prefix = "reference/playable-specialization/";
    private const string MediaPrefix = "reference/playable-specialization-media/";
    private const string ManifestName = "reference/playable-specialization/index.json";

    public async Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct)
    {
        // Fast path: single GET of the manifest produced by the ingester (Phase 3).
        var manifest = await blobs.GetAsync<List<SpecializationIndexEntry>>(ManifestName, ct);
        if (manifest is not null)
        {
            return manifest
                .Select(e => new SpecializationDto(e.Id, e.Name, e.ClassId, e.Role, e.IconUrl))
                .ToList();
        }

        // Fallback: enumerate per-id detail blobs + tolerantly fetch media for icons.
        // Runs on the first deploy before the ingester has emitted a manifest.
        var rows = new List<SpecializationDto>();
        await foreach (var detail in blobs.ListAsync<PlayableSpecializationBlob>(Prefix, ct))
        {
            var iconUrl = await ResolveIconUrlAsync(detail.Id, ct);
            var role = ToRole(detail.Role?.Type ?? "");
            var classId = detail.PlayableClass?.Id ?? 0;
            rows.Add(new SpecializationDto(detail.Id, detail.Name, classId, role, iconUrl));
        }
        return rows;
    }

    private async Task<string?> ResolveIconUrlAsync(int specId, CancellationToken ct)
    {
        var media = await blobs.GetAsync<MediaAssetsBlob>($"{MediaPrefix}{specId}.json", ct);
        if (media?.Assets is null) return null;

        foreach (var asset in media.Assets)
        {
            if (asset.Key == "icon" && !string.IsNullOrEmpty(asset.Value))
                return asset.Value;
        }
        return null;
    }

    // Mirrors ReferenceSync.ToRole: Blizzard's role.type ("DAMAGE", "HEALER", "TANK", ...)
    // collapses to the three game roles the frontend renders.
    private static string ToRole(string blizzardRoleType) => blizzardRoleType switch
    {
        "HEALER" => "HEALER",
        "TANK" => "TANK",
        _ => "DPS",
    };
}
