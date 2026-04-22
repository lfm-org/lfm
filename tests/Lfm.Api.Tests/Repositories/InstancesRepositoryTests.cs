// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Runtime.CompilerServices;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Repositories;

/// <summary>
/// Covers <see cref="InstancesRepository.ListAsync"/> against a mocked
/// <see cref="IBlobReferenceClient"/>: both the manifest fast path and the
/// per-id detail fallback, plus the portrait-media lookup and its tolerance
/// of missing blobs.
/// </summary>
public class InstancesRepositoryTests
{
    private static IAsyncEnumerable<T> AsAsync<T>(params T[] items) => AsAsyncImpl(items);

    private static async IAsyncEnumerable<T> AsAsyncImpl<T>(
        T[] items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }

    // ── Manifest fast path ───────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_returns_manifest_rows_one_per_mode_with_portrait_url()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                "reference/journal-instance/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceIndexEntry>
            {
                new(
                    Id: 1200,
                    Name: "Liberation of Undermine",
                    Modes: new[] { new InstanceIndexMode("NORMAL:25"), new InstanceIndexMode("HEROIC:25") },
                    Expansion: "The War Within",
                    PortraitUrl: "https://render.worldofwarcraft.com/tile.jpg"),
            });
        var repo = new InstancesRepository(blobs.Object);

        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Collection(rows,
            r =>
            {
                Assert.Equal("1200:NORMAL:25", r.Id);
                Assert.Equal(1200, r.InstanceNumericId);
                Assert.Equal("Liberation of Undermine", r.Name);
                Assert.Equal("NORMAL:25", r.ModeKey);
                Assert.Equal("The War Within", r.Expansion);
                Assert.Equal("https://render.worldofwarcraft.com/tile.jpg", r.PortraitUrl);
            },
            r =>
            {
                Assert.Equal("1200:HEROIC:25", r.Id);
                Assert.Equal(1200, r.InstanceNumericId);
                Assert.Equal("HEROIC:25", r.ModeKey);
                Assert.Equal("https://render.worldofwarcraft.com/tile.jpg", r.PortraitUrl);
            });

        blobs.Verify(b => b.ListAsync<JournalInstanceBlob>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListAsync_manifest_row_with_no_modes_emits_unknown_sentinel()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                "reference/journal-instance/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceIndexEntry>
            {
                new(Id: 42, Name: "Solo Dungeon", Modes: null, Expansion: "Test", PortraitUrl: null),
            });
        var repo = new InstancesRepository(blobs.Object);

        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("42:UNKNOWN:0", row.Id);
        Assert.Equal("UNKNOWN:0", row.ModeKey);
        Assert.Null(row.PortraitUrl);
    }

    // ── Fallback path (no manifest yet) ──────────────────────────────────────

    [Fact]
    public async Task ListAsync_falls_back_to_per_id_blobs_and_resolves_tile_portrait()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                "reference/journal-instance/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InstanceIndexEntry>?)null);

        blobs.Setup(b => b.ListAsync<JournalInstanceBlob>(
                "reference/journal-instance/", It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new JournalInstanceBlob(
                Id: 67,
                Name: "The Stonecore",
                Expansion: new JournalInstanceExpansionBlob("Cataclysm"),
                Modes: new[]
                {
                    new JournalInstanceModeBlob(new JournalModeRefBlob("NORMAL"), 5),
                })));

        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                "reference/journal-instance-media/67.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaAssetsBlob(new[]
            {
                new MediaAssetBlob("tile", "https://render.worldofwarcraft.com/stonecore-tile.jpg"),
                new MediaAssetBlob("image", "https://render.worldofwarcraft.com/stonecore-image.jpg"),
            }));

        var repo = new InstancesRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("67:NORMAL:5", row.Id);
        Assert.Equal(67, row.InstanceNumericId);
        Assert.Equal("The Stonecore", row.Name);
        Assert.Equal("Cataclysm", row.Expansion);
        Assert.Equal("https://render.worldofwarcraft.com/stonecore-tile.jpg", row.PortraitUrl);
    }

    // ── Regression for the composite-Id crash ────────────────────────────────

    [Fact]
    public async Task ListAsync_InstanceNumericId_is_the_plain_numeric_id_not_parseable_composite()
    {
        // Regression for the create-run / edit-run submit crash: callers used
        // to run Convert.ToInt32(dto.Id), which throws FormatException because
        // dto.Id is a composite like "1200:HEROIC:25". The InstanceNumericId
        // field exists to give callers the integer without string parsing.
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                "reference/journal-instance/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceIndexEntry>
            {
                new(Id: 1200, Name: "X", Modes: new[] { new InstanceIndexMode("HEROIC:25") }, Expansion: null, PortraitUrl: null),
            });

        var repo = new InstancesRepository(blobs.Object);
        var row = Assert.Single(await repo.ListAsync(CancellationToken.None));

        Assert.Equal("1200:HEROIC:25", row.Id);
        Assert.Equal(1200, row.InstanceNumericId);
        Assert.Throws<FormatException>(() => Convert.ToInt32(row.Id));
    }

    [Fact]
    public async Task ListAsync_falls_back_and_uses_image_when_tile_missing()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InstanceIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<JournalInstanceBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new JournalInstanceBlob(
                Id: 67, Name: "X",
                Modes: new[] { new JournalInstanceModeBlob(new JournalModeRefBlob("NORMAL"), 25) })));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaAssetsBlob(new[]
            {
                new MediaAssetBlob("image", "https://render.worldofwarcraft.com/legacy.jpg"),
            }));

        var repo = new InstancesRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Equal("https://render.worldofwarcraft.com/legacy.jpg", Assert.Single(rows).PortraitUrl);
    }

    [Fact]
    public async Task ListAsync_falls_back_and_returns_null_portrait_when_media_blob_missing()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InstanceIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<JournalInstanceBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new JournalInstanceBlob(
                Id: 67, Name: "X",
                Modes: new[] { new JournalInstanceModeBlob(new JournalModeRefBlob("NORMAL"), 25) })));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAssetsBlob?)null);

        var repo = new InstancesRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Null(Assert.Single(rows).PortraitUrl);
    }

    [Fact]
    public async Task ListAsync_falls_back_and_emits_unknown_sentinel_for_instance_with_no_modes()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<InstanceIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<InstanceIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<JournalInstanceBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new JournalInstanceBlob(Id: 42, Name: "Solo", Modes: null)));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAssetsBlob?)null);

        var repo = new InstancesRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("42:UNKNOWN:0", row.Id);
        Assert.Equal("UNKNOWN:0", row.ModeKey);
        Assert.Equal("", row.Expansion);
    }
}
