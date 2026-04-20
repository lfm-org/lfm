// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Lfm.Api.Options;
using Lfm.Api.Serialization;
using Lfm.Contracts.Specializations;

namespace Lfm.Api.Repositories;

/// <summary>
/// Projection row for <see cref="SpecializationsRepository.ListAsync"/>.
///
/// Mirrors <c>InstanceListRow</c>: we cannot project Cosmos directly into
/// <see cref="SpecializationDto"/> because legacy documents store <c>c.name</c>
/// as Blizzard's localized-object shape, and <see cref="SpecializationDto"/>
/// has no converter. Same root cause as the 2026-04-20 /api/guild and
/// /api/instances incidents.
/// </summary>
internal sealed record SpecializationListRow(
    int Id,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    int ClassId,
    string Role,
    string? IconUrl);

public sealed class SpecializationsRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : ISpecializationsRepository
{
    private const string ContainerName = "specializations";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct)
    {
        // Project specId → id so that the projection row's Id (int) receives the numeric
        // spec id rather than the Cosmos string document id.
        var query = new QueryDefinition("SELECT c.specId AS id, c.name, c.classId, c.role, c.iconUrl FROM c");
        var results = new List<SpecializationDto>();
        using var iterator = _container.GetItemQueryIterator<SpecializationListRow>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            foreach (var row in page)
            {
                results.Add(new SpecializationDto(row.Id, row.Name, row.ClassId, row.Role, row.IconUrl));
            }
        }
        return results;
    }

    public async Task UpsertBatchAsync(IEnumerable<SpecializationDocument> documents, CancellationToken ct)
    {
        foreach (var doc in documents)
        {
            await _container.UpsertItemAsync(doc, new Microsoft.Azure.Cosmos.PartitionKey(doc.Id), cancellationToken: ct);
        }
    }
}
