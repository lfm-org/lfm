// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsRepositoryGuardTests
{
    [Fact]
    public async Task ListForGuildAsync_throws_on_non_numeric_guild_id()
    {
        var clientMock = new Mock<CosmosClient>(MockBehavior.Loose);
        clientMock.Setup(c => c.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new Mock<Container>(MockBehavior.Loose).Object);

        var opts = Microsoft.Extensions.Options.Options.Create(new CosmosOptions
        {
            Endpoint = "https://test.documents.azure.com",
            DatabaseName = "lfm-test",
        });
        var repo = new RunsRepository(clientMock.Object, opts, NullLogger<RunsRepository>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.ListForGuildAsync(
                guildId: "not-a-number",
                battleNetId: "bnet-1",
                top: 50,
                continuationToken: null,
                ct: CancellationToken.None));
    }
}
