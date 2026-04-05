using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Repositories;

public sealed class RaidersRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts) : IRaidersRepository
{
    private const string ContainerName = "raiders";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<RaiderDocument?> GetByBattleNetIdAsync(string battleNetId, CancellationToken ct)
    {
        try
        {
            var response = await _container.ReadItemAsync<RaiderDocument>(
                battleNetId,
                new PartitionKey(battleNetId),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
