// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Moq;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsRepositoryConcurrencyTests
{
    // Anchored to UtcNow so these fixtures never become time bombs against a
    // future-dated assertion. See issue #49.
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");
    private static readonly string PastCreatedAt =
        DateTimeOffset.UtcNow.AddDays(-14).ToString("o");

    private static RunDocument MakeRunDoc(string id = "run-1", string? etag = "\"etag-1\"") =>
        new RunDocument(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "NORMAL:10",
            Visibility: "PUBLIC",
            CreatorGuild: "Test Guild",
            CreatorGuildId: null,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: PastCreatedAt,
            Ttl: 86400,
            RunCharacters: [],
            ETag: etag);

    [Fact]
    public async Task UpdateAsync_throws_ConcurrencyConflictException_on_etag_mismatch()
    {
        var container = new Mock<Container>();
        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<RunDocument>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException(
                "Precondition failed",
                HttpStatusCode.PreconditionFailed,
                subStatusCode: 0,
                activityId: "test",
                requestCharge: 0));

        var client = new Mock<CosmosClient>();
        client.Setup(c => c.GetContainer("testdb", "runs")).Returns(container.Object);

        var opts = Microsoft.Extensions.Options.Options.Create(new CosmosOptions { Endpoint = "https://test.documents.azure.com", DatabaseName = "testdb" });
        var repo = new RunsRepository(client.Object, opts);

        var run = MakeRunDoc(etag: "\"stale-etag\"");

        var act = () => repo.UpdateAsync(run, null, CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(act);
    }

    [Fact]
    public async Task UpdateAsync_passes_etag_in_request_options()
    {
        var run = MakeRunDoc(etag: "\"my-etag\"");

        var responseMock = new Mock<ItemResponse<RunDocument>>();
        responseMock.Setup(r => r.Resource).Returns(run);
        responseMock.Setup(r => r.ETag).Returns("\"new-etag\"");

        var container = new Mock<Container>();
        container
            .Setup(c => c.ReplaceItemAsync(
                It.IsAny<RunDocument>(),
                "run-1",
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "\"my-etag\""),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseMock.Object);

        var client = new Mock<CosmosClient>();
        client.Setup(c => c.GetContainer("testdb", "runs")).Returns(container.Object);

        var opts = Microsoft.Extensions.Options.Options.Create(new CosmosOptions { Endpoint = "https://test.documents.azure.com", DatabaseName = "testdb" });
        var repo = new RunsRepository(client.Object, opts);

        var result = await repo.UpdateAsync(run, null, CancellationToken.None);

        Assert.Equal("\"new-etag\"", result.ETag);

        container.Verify(
            c => c.ReplaceItemAsync(
                It.IsAny<RunDocument>(),
                "run-1",
                It.IsAny<PartitionKey>(),
                It.Is<ItemRequestOptions>(o => o.IfMatchEtag == "\"my-etag\""),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
