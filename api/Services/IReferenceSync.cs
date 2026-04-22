// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Admin;

namespace Lfm.Api.Services;

/// <summary>
/// Synchronises WoW reference data from the Blizzard Game Data API into Cosmos.
///
/// Mirrors the entity-sync-defs pattern in reference-sync-blizzard.ts /
/// reference-sync-live.ts. Each entity sync is attempted independently;
/// failures are captured per-entity so that one failure does not abort the others.
///
/// Entities synced (in order):
///   1. instances       — journal-instance index + details → instances container
///   2. specializations — playable-specialization index + details + media → specializations container
/// </summary>
public interface IReferenceSync
{
    /// <summary>
    /// Fetches all entities from the Blizzard API and upserts them into Cosmos.
    /// Never throws; individual entity failures are reported in the returned results.
    /// </summary>
    /// <param name="progress">Optional progress sink. When supplied, each entity's
    /// sync emits a <c>start</c> event (total known from the Blizzard index), a
    /// <c>progress</c> event per item (carrying the current-item name), and an
    /// <c>end</c> event (carrying the final per-entity status string). Intended
    /// for streaming the sync to the admin UI — see
    /// <c>WowReferenceRefreshFunction</c>, which serialises the events as NDJSON.</param>
    Task<WowReferenceRefreshResponse> SyncAllAsync(
        CancellationToken ct,
        IProgress<WowReferenceRefreshProgress>? progress = null);
}
