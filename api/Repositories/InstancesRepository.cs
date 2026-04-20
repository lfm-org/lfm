// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Lfm.Api.Options;
using Lfm.Api.Serialization;
using Lfm.Contracts.Instances;

namespace Lfm.Api.Repositories;

/// <summary>
/// Projection row for <see cref="InstancesRepository.ListAsync"/>.
///
/// We cannot project the Cosmos query directly into <see cref="InstanceDto"/>
/// because legacy documents store <c>c.name</c> as Blizzard's localized-object
/// shape (<c>{ "en_US": "…", … }</c>). <see cref="InstanceDto"/> lives in
/// <c>Lfm.Contracts</c> and has no JSON converter, so Newtonsoft (via the
/// Cosmos SDK) raised <c>JsonReaderException</c> on every /api/instances call —
/// the same class of failure as the 2026-04-20 /api/guild incident.
/// </summary>
internal sealed record InstanceListRow(
    string Id,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    string ModeKey,
    string Expansion);

public sealed class InstancesRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : IInstancesRepository
{
    private const string ContainerName = "instances";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct)
    {
        // Each row is one (instanceId, modeKey) pair. The document id is "{instanceId}:{modeKey}".
        // We project instanceId as 'id' so that the projection row's Id is the bare instance id.
        var query = new QueryDefinition("SELECT c.instanceId AS id, c.name, c.modeKey, c.expansion FROM c");
        var results = new List<InstanceDto>();
        using var iterator = _container.GetItemQueryIterator<InstanceListRow>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            foreach (var row in page)
            {
                results.Add(new InstanceDto(row.Id, row.Name, row.ModeKey, row.Expansion));
            }
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
