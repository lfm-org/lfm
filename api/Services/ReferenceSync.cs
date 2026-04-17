// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Admin;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IReferenceSync"/>.
///
/// Syncs two entity types in order:
///   1. instances       — journal-instance index + details → instances Cosmos container
///   2. specializations — playable-specialization index + details + media → specializations container
///
/// Per-entity failures are caught and recorded; the remaining entities are still attempted.
/// Mirrors the resilient loop in syncReferenceEntities (reference-sync-live.ts).
///
/// TODO: add classes and races syncs once those containers are added to the C# stack.
/// </summary>
public sealed class ReferenceSync(
    IBlizzardGameDataClient gameData,
    IInstancesRepository instancesRepo,
    ISpecializationsRepository specsRepo,
    ILogger<ReferenceSync> logger) : IReferenceSync
{
    /// <inheritdoc/>
    public async Task<WowUpdateResponse> SyncAllAsync(CancellationToken ct)
    {
        string? token = null;
        var results = new List<WowUpdateEntityResult>();

        // --- instances ---
        try
        {
            token ??= await gameData.GetClientCredentialsTokenAsync(ct);
            var docs = await SyncInstancesAsync(token, ct);
            results.Add(new WowUpdateEntityResult("instances", $"synced ({docs} docs)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync instances");
            results.Add(new WowUpdateEntityResult("instances", $"failed: {ex.Message}"));
        }

        // --- specializations ---
        try
        {
            token ??= await gameData.GetClientCredentialsTokenAsync(ct);
            var docs = await SyncSpecializationsAsync(token, ct);
            results.Add(new WowUpdateEntityResult("specializations", $"synced ({docs} docs)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync specializations");
            results.Add(new WowUpdateEntityResult("specializations", $"failed: {ex.Message}"));
        }

        return new WowUpdateResponse(results);
    }

    // ---------------------------------------------------------------------------
    // Instance sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncInstancesAsync(string token, CancellationToken ct)
    {
        var index = await gameData.GetJournalInstanceIndexAsync(token, ct);
        var documents = new List<InstanceDocument>();

        foreach (var entry in index.Instances)
        {
            BlizzardJournalInstanceDetail detail;
            while (true)
            {
                try
                {
                    detail = await gameData.GetJournalInstanceAsync(entry.Id, token, ct);
                    break;
                }
                catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping instance {Id}: detail fetch failed", entry.Id);
                    detail = null!;
                    break;
                }
            }
            if (detail is null) continue;

            var expansion = detail.Expansion?.Name ?? "";
            var modes = detail.Modes ?? [];

            var instanceIdStr = detail.Id.ToString();

            if (modes.Count == 0)
            {
                // No modes — store a sentinel record with modeKey "UNKNOWN:0"
                documents.Add(new InstanceDocument(
                    Id: $"{instanceIdStr}:UNKNOWN:0",
                    InstanceId: instanceIdStr,
                    Name: detail.Name,
                    ModeKey: "UNKNOWN:0",
                    Expansion: expansion));
            }
            else
            {
                foreach (var mode in modes)
                {
                    var modeKey = $"{mode.Mode.Type}:{mode.Players ?? 0}";
                    documents.Add(new InstanceDocument(
                        Id: $"{instanceIdStr}:{modeKey}",
                        InstanceId: instanceIdStr,
                        Name: detail.Name,
                        ModeKey: modeKey,
                        Expansion: expansion));
                }
            }
        }

        await instancesRepo.UpsertBatchAsync(documents, ct);
        return documents.Count;
    }

    // ---------------------------------------------------------------------------
    // Specialization sync
    // ---------------------------------------------------------------------------

    private async Task<int> SyncSpecializationsAsync(string token, CancellationToken ct)
    {
        var index = await gameData.GetPlayableSpecIndexAsync(token, ct);
        var documents = new List<SpecializationDocument>();

        foreach (var entry in index.CharacterSpecializations)
        {
            BlizzardPlayableSpecDetail detail;
            while (true)
            {
                try
                {
                    detail = await gameData.GetPlayableSpecAsync(entry.Id, token, ct);
                    break;
                }
                catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping specialization {Id}: detail fetch failed", entry.Id);
                    detail = null!;
                    break;
                }
            }
            if (detail is null) continue;

            // Media (icon URL) — keep the existing best-effort try/catch; no retry on 429.
            string? iconUrl = null;
            try
            {
                var media = await gameData.GetPlayableSpecMediaAsync(entry.Id, token, ct);
                iconUrl = media.Assets?.FirstOrDefault(a => a.Key == "icon")?.Value;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not fetch media for spec {Id}", entry.Id);
            }

            var role = ToRole(detail.Role.Type);
            documents.Add(new SpecializationDocument(
                Id: entry.Id.ToString(),
                SpecId: entry.Id,
                Name: detail.Name,
                ClassId: detail.PlayableClass.Id,
                Role: role,
                IconUrl: iconUrl));
        }

        await specsRepo.UpsertBatchAsync(documents, ct);
        return documents.Count;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string ToRole(string blizzardRoleType) => blizzardRoleType switch
    {
        "HEALER" => "HEALER",
        "TANK" => "TANK",
        _ => "DPS",
    };
}
