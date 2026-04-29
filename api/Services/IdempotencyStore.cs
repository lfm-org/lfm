// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text.Json.Serialization;
using Lfm.Api.Options;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Durable replay cache for <c>Idempotency-Key</c>-carrying mutations. Stores
/// a minimal envelope (status + response shape hint + created timestamp) per
/// (battleNetId, idempotencyKey) pair so retries return the original response
/// description instead of re-executing the handler. Entries TTL out after
/// <see cref="IdempotencyOptions.TtlSeconds"/>.
/// </summary>
public sealed record IdempotencyEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("battleNetId")] string BattleNetId,
    [property: JsonPropertyName("idempotencyKey")] string IdempotencyKey,
    [property: JsonPropertyName("statusCode")] int StatusCode,
    [property: JsonPropertyName("etag")] string? ETag,
    [property: JsonPropertyName("bodyHash")] string? BodyHash,
    [property: JsonPropertyName("createdAt")] string CreatedAt,
    [property: JsonPropertyName("ttl")] int Ttl);

public interface IIdempotencyStore
{
    /// <summary>
    /// Returns the stored entry for (<paramref name="battleNetId"/>,
    /// <paramref name="key"/>) or null if none exists (or it has TTL'd out).
    /// </summary>
    Task<IdempotencyEntry?> TryGetAsync(string battleNetId, string key, CancellationToken ct);

    /// <summary>
    /// Persists a new idempotency entry. Overwrites any existing entry for the
    /// same pair — the middleware writes only once per request, so the only
    /// way to reach this is the first call of a retry chain.
    /// </summary>
    Task PutAsync(IdempotencyEntry entry, CancellationToken ct);

    /// <summary>
    /// Removes every idempotency entry belonging to <paramref name="battleNetId"/>.
    /// Called from the GDPR deletion flow so the cache cannot outlive the
    /// raider document itself.
    /// </summary>
    Task PurgeForActorAsync(string battleNetId, CancellationToken ct);
}

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly Container _container;
    private readonly IdempotencyOptions _options;

    public IdempotencyStore(CosmosClient client, IOptions<CosmosOptions> cosmosOpts, IOptions<IdempotencyOptions> idempotencyOpts)
    {
        _options = idempotencyOpts.Value;
        _container = client.GetContainer(cosmosOpts.Value.DatabaseName, _options.ContainerName);
    }

    public async Task<IdempotencyEntry?> TryGetAsync(string battleNetId, string key, CancellationToken ct)
    {
        try
        {
            var response = await _container.ReadItemAsync<IdempotencyEntry>(
                DocumentId(battleNetId, key),
                new PartitionKey(battleNetId),
                cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task PutAsync(IdempotencyEntry entry, CancellationToken ct)
    {
        // UpsertItemAsync is safe here: PutAsync is only invoked on the first
        // request of a retry chain (middleware writes once after the handler
        // completes). A concurrent duplicate POST that races to put the same
        // key is a user/client bug; Cosmos will happily overwrite with an
        // equivalent entry. The TTL is applied server-side from the document's
        // `ttl` field.
        await _container.UpsertItemAsync(
            entry,
            new PartitionKey(entry.BattleNetId),
            cancellationToken: ct);
    }

    public async Task PurgeForActorAsync(string battleNetId, CancellationToken ct)
    {
        // Delete every entry in the partition. The partition is small (bounded
        // by retry burst) so a cross-partition query is not needed — we just
        // enumerate the partition and delete each id we find.
        var query = new QueryDefinition("SELECT c.id FROM c");
        using var iterator = _container.GetItemQueryIterator<IdempotencyIdOnly>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(battleNetId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            foreach (var row in page)
            {
                try
                {
                    await _container.DeleteItemAsync<IdempotencyEntry>(
                        row.Id,
                        new PartitionKey(battleNetId),
                        cancellationToken: ct);
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // TTL'd out between query and delete — treat as success.
                }
            }
        }
    }

    internal static string DocumentId(string battleNetId, string idempotencyKey)
        => $"{battleNetId}:{idempotencyKey}";

    private sealed record IdempotencyIdOnly([property: JsonPropertyName("id")] string Id);
}
