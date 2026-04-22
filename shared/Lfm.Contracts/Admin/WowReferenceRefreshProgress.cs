// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Admin;

/// <summary>
/// One progress event emitted by <c>ReferenceSync</c> during a refresh.
/// Stream-friendly — serialised as one NDJSON line on the wire so the admin
/// UI can show "instances 73/510 (Siege of Boralus)" while the sync is in
/// flight, instead of waiting 1–2 minutes for the final result.
/// </summary>
/// <param name="Entity">Which sync block emitted the event — <c>"instances"</c>,
/// <c>"specializations"</c>, or <c>"expansions"</c>.</param>
/// <param name="Phase">Phase within that entity's sync — <c>"start"</c> (totals
/// announced), <c>"progress"</c> (one item just processed), or <c>"end"</c>
/// (the entity's manifest has been written and its <c>Status</c> string is final).</param>
/// <param name="Processed">How many items of the entity have been processed so far
/// (0 on start, equals <paramref name="Total"/> on end).</param>
/// <param name="Total">Total items the entity will process (known upfront from the
/// Blizzard index).</param>
/// <param name="Current">Human-friendly name of the item just processed on a
/// <c>"progress"</c> event (e.g. an instance name). Null on start / end.</param>
/// <param name="Status">Final per-entity status string on <c>"end"</c> events —
/// mirrors <c>WowReferenceRefreshEntityResult.Status</c> (<c>"synced (N docs)"</c>
/// or <c>"failed: …"</c>). Null on start / progress.</param>
public sealed record WowReferenceRefreshProgress(
    string Entity,
    string Phase,
    int Processed,
    int Total,
    string? Current = null,
    string? Status = null);
