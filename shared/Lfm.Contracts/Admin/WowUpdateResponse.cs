// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Admin;

/// <summary>
/// Response returned by POST /api/wow/update.
/// Each entry reports the name and outcome of one entity sync operation.
/// </summary>
public sealed record WowUpdateResponse(IReadOnlyList<WowUpdateEntityResult> Results);

/// <summary>
/// The outcome for a single entity sync (e.g. "instances", "specializations").
/// Status is a human-readable string such as "synced (12 docs)" or "failed: ...".
/// </summary>
public sealed record WowUpdateEntityResult(string Name, string Status);
