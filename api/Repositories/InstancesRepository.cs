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
        var query = new QueryDefinition("SELECT c.id, c.name, c.modeKey, c.expansion FROM c");
        var results = new List<InstanceDto>();
        using var iterator = _container.GetItemQueryIterator<InstanceDto>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
        }
        return results;
    }
}
