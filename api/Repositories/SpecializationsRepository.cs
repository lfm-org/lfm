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
        var query = new QueryDefinition("SELECT c.id, c.name, c.classId, c.role, c.iconUrl FROM c");
        var results = new List<SpecializationDto>();
        using var iterator = _container.GetItemQueryIterator<SpecializationDto>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
        }
        return results;
    }
}
