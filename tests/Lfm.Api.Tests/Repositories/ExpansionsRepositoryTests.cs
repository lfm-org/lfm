// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Repositories;

public class ExpansionsRepositoryTests
{
    [Fact]
    public async Task ListAsync_returns_manifest_rows_in_blizzard_order()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<ExpansionIndexEntry>>(
                "reference/journal-expansion/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExpansionIndexEntry>
            {
                new(Id: 68, Name: "Classic"),
                new(Id: 67, Name: "The Burning Crusade"),
                new(Id: 505, Name: "The War Within"),
            });

        var repo = new ExpansionsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Collection(rows,
            r => { Assert.Equal(68, r.Id); Assert.Equal("Classic", r.Name); },
            r => { Assert.Equal(67, r.Id); Assert.Equal("The Burning Crusade", r.Name); },
            r => { Assert.Equal(505, r.Id); Assert.Equal("The War Within", r.Name); });
    }

    [Fact]
    public async Task ListAsync_returns_empty_list_when_manifest_missing()
    {
        // First deploy: manifest blob hasn't been written yet. Repository
        // should return an empty list rather than throw.
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<ExpansionIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ExpansionIndexEntry>?)null);

        var repo = new ExpansionsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Empty(rows);
    }
}
