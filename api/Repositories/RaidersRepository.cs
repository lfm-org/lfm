// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Repositories;

public sealed class RaidersRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts, ILogger<RaidersRepository> logger) : IRaidersRepository
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
            logger.LogRequestCharge(response, "read", ContainerName, battleNetId);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(RaiderDocument raider, CancellationToken ct)
    {
        var response = await _container.UpsertItemAsync(
            raider,
            new PartitionKey(raider.BattleNetId),
            cancellationToken: ct);
        logger.LogRequestCharge(response, "upsert", ContainerName, raider.BattleNetId);
    }

    public async Task<RaiderDocument> ReplaceAsync(RaiderDocument raider, string ifMatchEtag, CancellationToken ct)
    {
        try
        {
            var options = new ItemRequestOptions { IfMatchEtag = ifMatchEtag };
            var response = await _container.ReplaceItemAsync(
                raider,
                raider.BattleNetId,
                new PartitionKey(raider.BattleNetId),
                options,
                ct);
            logger.LogRequestCharge(response, "replace", ContainerName, raider.BattleNetId);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }

    public async Task DeleteAsync(string battleNetId, CancellationToken ct)
    {
        try
        {
            var response = await _container.DeleteItemAsync<RaiderDocument>(
                battleNetId,
                new PartitionKey(battleNetId),
                cancellationToken: ct);
            logger.LogRequestCharge(response, "delete", ContainerName, battleNetId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Idempotent: raider already deleted, treat as success.
        }
    }

    public async Task<IReadOnlyList<RaiderDocument>> ListExpiredAsync(string cutoff, CancellationToken ct)
    {
        // Cross-partition query: raiders inactive for > 90 days or with no lastSeenAt.
        // Mirrors the TS query in raider-cleanup.ts.
        // Only id and battleNetId are projected to minimise RU cost.
        const string query = """
            SELECT c.id, c.battleNetId FROM c
            WHERE c.lastSeenAt < @cutoff OR NOT IS_DEFINED(c.lastSeenAt)
            """;

        var feedIterator = _container.GetItemQueryIterator<RaiderDocument>(
            new QueryDefinition(query).WithParameter("@cutoff", cutoff));

        var results = new List<RaiderDocument>();
        var totalRu = 0.0;
        while (feedIterator.HasMoreResults)
        {
            var page = await feedIterator.ReadNextAsync(ct);
            totalRu += page.RequestCharge;
            results.AddRange(page);
        }
        logger.LogRequestChargeTotal("list-expired", ContainerName, totalRu);
        return results;
    }
}
