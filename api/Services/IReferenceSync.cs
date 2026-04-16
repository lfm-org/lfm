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
    Task<WowUpdateResponse> SyncAllAsync(CancellationToken ct);
}
