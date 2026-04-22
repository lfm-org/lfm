// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Expansions;

namespace Lfm.Api.Repositories;

/// <summary>
/// Reads the journal-expansion manifest written by <c>ReferenceSync</c>
/// at <c>reference/journal-expansion/index.json</c>.
/// </summary>
public interface IExpansionsRepository
{
    /// <summary>
    /// Returns every WoW expansion known to the Blizzard Game Data API, in
    /// the canonical order Blizzard returns. Empty list if the manifest
    /// blob is absent (pre-ingest).
    /// </summary>
    Task<IReadOnlyList<ExpansionDto>> ListAsync(CancellationToken ct);
}
