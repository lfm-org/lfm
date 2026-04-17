// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;
using Lfm.Contracts.Specializations;

namespace Lfm.Api.Repositories;

public sealed class SpecializationsRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : ISpecializationsRepository
{
    private const string ContainerName = "specializations";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct)
    {
        // Project specId → id so that SpecializationDto.Id (int) receives the numeric
        // spec id rather than the Cosmos string document id.
        var query = new QueryDefinition("SELECT c.specId AS id, c.name, c.classId, c.role, c.iconUrl FROM c");
        var results = new List<SpecializationDto>();
        using var iterator = _container.GetItemQueryIterator<SpecializationDto>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
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
