// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Repositories;

public sealed class GuildRepository(CosmosClient client, IOptions<CosmosOptions> cosmosOpts, ILogger<GuildRepository> logger) : IGuildRepository
{
    private const string ContainerName = "guilds";
    private readonly Container _container = client.GetContainer(cosmosOpts.Value.DatabaseName, ContainerName);

    public async Task<GuildDocument?> GetAsync(string guildId, CancellationToken ct)
    {
        try
        {
            var response = await _container.ReadItemAsync<GuildDocument>(
                guildId,
                new PartitionKey(guildId),
                cancellationToken: ct);
            logger.LogRequestCharge(response, "read", ContainerName, guildId);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertAsync(GuildDocument doc, CancellationToken ct)
    {
        var response = await _container.UpsertItemAsync(
            doc,
            new PartitionKey(doc.Id),
            cancellationToken: ct);
        logger.LogRequestCharge(response, "upsert", ContainerName, doc.Id);
    }

    public async Task<GuildDocument> ReplaceAsync(GuildDocument doc, string ifMatchEtag, CancellationToken ct)
    {
        try
        {
            var options = new ItemRequestOptions { IfMatchEtag = ifMatchEtag };
            var response = await _container.ReplaceItemAsync(
                doc,
                doc.Id,
                new PartitionKey(doc.Id),
                options,
                ct);
            logger.LogRequestCharge(response, "replace", ContainerName, doc.Id);
            return response.Resource with { ETag = response.ETag };
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            throw new ConcurrencyConflictException(ex);
        }
    }
}
