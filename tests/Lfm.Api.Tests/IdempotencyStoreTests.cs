// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.Api.Options;
using Lfm.Api.Services;
using Microsoft.Azure.Cosmos;
using Moq;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

public class IdempotencyStoreTests
{
    private static (IdempotencyStore store, Mock<Container> container) MakeStore()
    {
        var container = new Mock<Container>();
        var client = new Mock<CosmosClient>();
        client.Setup(c => c.GetContainer("testdb", "idempotency")).Returns(container.Object);

        var cosmos = MSOptions.Create(new CosmosOptions { Endpoint = "https://test.documents.azure.com", DatabaseName = "testdb" });
        var idemOpts = MSOptions.Create(new IdempotencyOptions { ContainerName = "idempotency", TtlSeconds = 86400 });

        return (new IdempotencyStore(client.Object, cosmos, idemOpts), container);
    }

    private static IdempotencyEntry MakeEntry(string battleNetId = "bnet-1", string key = "abc") =>
        new(
            Id: $"{battleNetId}:{key}",
            BattleNetId: battleNetId,
            IdempotencyKey: key,
            StatusCode: 201,
            ETag: null,
            BodyHash: "hash",
            CreatedAt: DateTimeOffset.UtcNow.ToString("o"),
            Ttl: 86400);

    [Fact]
    public async Task TryGetAsync_returns_null_on_404()
    {
        var (store, container) = MakeStore();
        container
            .Setup(c => c.ReadItemAsync<IdempotencyEntry>(
                It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("not found", HttpStatusCode.NotFound, 0, "a", 0));

        var result = await store.TryGetAsync("bnet-1", "key-1", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_returns_entry_on_hit()
    {
        var (store, container) = MakeStore();
        var entry = MakeEntry();

        var response = new Mock<ItemResponse<IdempotencyEntry>>();
        response.Setup(r => r.Resource).Returns(entry);
        container
            .Setup(c => c.ReadItemAsync<IdempotencyEntry>(
                "bnet-1:abc", new PartitionKey("bnet-1"), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object);

        var result = await store.TryGetAsync("bnet-1", "abc", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(201, result!.StatusCode);
        Assert.Equal("hash", result.BodyHash);
    }

    [Fact]
    public async Task PutAsync_upserts_with_partition_key_set_to_battleNetId()
    {
        var (store, container) = MakeStore();
        var entry = MakeEntry();

        var response = new Mock<ItemResponse<IdempotencyEntry>>();
        container
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<IdempotencyEntry>(),
                It.Is<PartitionKey?>(p => p.HasValue && p.Value.Equals(new PartitionKey("bnet-1"))),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response.Object)
            .Verifiable();

        await store.PutAsync(entry, CancellationToken.None);

        container.Verify();
    }

    // PurgeForActorAsync is integration-tested rather than unit-tested because
    // Moq cannot cleanly intercept GetItemQueryIterator with an internal
    // generic parameter (the store's id-only projection type). The E2E suite
    // exercises the real purge against Azurite-backed Cosmos in MeDelete.
}
