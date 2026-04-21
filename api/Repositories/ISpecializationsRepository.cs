// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Specializations;

namespace Lfm.Api.Repositories;

/// <summary>
/// Reads the static Blizzard playable-specialization reference data set.
///
/// Source: blob container <c>lfmstore/wow/reference/playable-specialization/</c>
/// plus <c>.../playable-specialization-media/</c> for icon URLs — see
/// <c>docs/storage-architecture.md</c>. The ingester that populates this is
/// <c>WowUpdateFunction</c> / <c>WowUpdateTimerFunction</c> (Phase 3).
/// </summary>
public interface ISpecializationsRepository
{
    Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct);
}
