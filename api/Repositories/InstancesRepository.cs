// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;
using Lfm.Contracts.Instances;

namespace Lfm.Api.Repositories;

public sealed class InstancesRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : IInstancesRepository
{
    private const string ContainerName = "instances";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct)
    {
        // Each row is one (instanceId, modeKey) pair. The document id is "{instanceId}:{modeKey}".
        // We project instanceId as 'id' so that InstanceDto.Id receives the instance id string.
        var query = new QueryDefinition("SELECT c.instanceId AS id, c.name, c.modeKey, c.expansion FROM c");
        var results = new List<InstanceDto>();
        using var iterator = _container.GetItemQueryIterator<InstanceDto>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
        }
        return results;
    }

    public async Task UpsertBatchAsync(IEnumerable<InstanceDocument> documents, CancellationToken ct)
    {
        foreach (var doc in documents)
        {
            await _container.UpsertItemAsync(doc, new Microsoft.Azure.Cosmos.PartitionKey(doc.Id), cancellationToken: ct);
        }
    }
}
