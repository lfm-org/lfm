// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Instances;

namespace Lfm.Api.Repositories;

/// <summary>
/// Reads the static Blizzard journal-instance reference data set.
///
/// Source: blob container <c>lfmstore/wow/reference/journal-instance/</c> — see
/// <c>docs/storage-architecture.md</c>. The ingester that populates this is
/// <c>WowUpdateFunction</c> / <c>WowUpdateTimerFunction</c> (Phase 3).
/// </summary>
public interface IInstancesRepository
{
    /// <summary>
    /// Returns one <see cref="InstanceDto"/> per (instance, mode) pair across every
    /// instance present in blob. An instance with no modes yields a single row with
    /// <c>ModeKey = "UNKNOWN:0"</c> so the frontend dropdown always has a selection.
    /// </summary>
    Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct);
}
