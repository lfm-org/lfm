// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Lfm.Contracts.Expansions;

namespace Lfm.Api.Repositories;

/// <summary>
/// Manifest entry emitted by the ingester at
/// <c>reference/journal-expansion/index.json</c>. Name mirrors the Blizzard
/// tier name verbatim.
/// </summary>
internal sealed record ExpansionIndexEntry(int Id, string Name);

public sealed class ExpansionsRepository(IBlobReferenceClient blobs) : IExpansionsRepository
{
    private const string ManifestName = "reference/journal-expansion/index.json";

    public async Task<IReadOnlyList<ExpansionDto>> ListAsync(CancellationToken ct)
    {
        var manifest = await blobs.GetAsync<List<ExpansionIndexEntry>>(ManifestName, ct);
        if (manifest is null) return [];
        return manifest
            .Select(e => new ExpansionDto(e.Id, e.Name))
            .ToList();
    }
}
