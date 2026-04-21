// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Runtime.CompilerServices;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Repositories;

/// <summary>
/// Covers <see cref="SpecializationsRepository.ListAsync"/> against a mocked
/// <see cref="IBlobReferenceClient"/>: the manifest fast path, the per-id
/// detail fallback, icon-media resolution, and the role-type mapping.
/// </summary>
public class SpecializationsRepositoryTests
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
    public async Task ListAsync_returns_manifest_rows_with_icon_url()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<SpecializationIndexEntry>>(
                "reference/playable-specialization/index.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SpecializationIndexEntry>
            {
                new(Id: 262, Name: "Elemental", ClassId: 7, Role: "DPS",
                    IconUrl: "https://render.worldofwarcraft.com/elemental.jpg"),
            });

        var repo = new SpecializationsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(262, row.Id);
        Assert.Equal("Elemental", row.Name);
        Assert.Equal(7, row.ClassId);
        Assert.Equal("DPS", row.Role);
        Assert.Equal("https://render.worldofwarcraft.com/elemental.jpg", row.IconUrl);

        blobs.Verify(b => b.ListAsync<PlayableSpecializationBlob>(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Fallback path (no manifest yet) ──────────────────────────────────────

    [Fact]
    public async Task ListAsync_falls_back_to_per_id_blobs_and_resolves_icon()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<SpecializationIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SpecializationIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<PlayableSpecializationBlob>(
                "reference/playable-specialization/", It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new PlayableSpecializationBlob(
                Id: 262,
                Name: "Elemental",
                PlayableClass: new PlayableClassRefBlob(7),
                Role: new PlayableSpecRoleRefBlob("DAMAGE"))));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                "reference/playable-specialization-media/262.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaAssetsBlob(new[]
            {
                new MediaAssetBlob("icon", "https://render.worldofwarcraft.com/elemental.jpg"),
            }));

        var repo = new SpecializationsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(262, row.Id);
        Assert.Equal("Elemental", row.Name);
        Assert.Equal(7, row.ClassId);
        Assert.Equal("DPS", row.Role);
        Assert.Equal("https://render.worldofwarcraft.com/elemental.jpg", row.IconUrl);
    }

    [Fact]
    public async Task ListAsync_falls_back_and_returns_null_icon_when_media_missing()
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<SpecializationIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SpecializationIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<PlayableSpecializationBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new PlayableSpecializationBlob(
                Id: 257, Name: "Holy",
                PlayableClass: new PlayableClassRefBlob(5),
                Role: new PlayableSpecRoleRefBlob("HEALER"))));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAssetsBlob?)null);

        var repo = new SpecializationsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Null(row.IconUrl);
        Assert.Equal("HEALER", row.Role);
    }

    // ── Role mapping (fallback path) ─────────────────────────────────────────

    [Theory]
    [InlineData("HEALER", "HEALER")]
    [InlineData("TANK", "TANK")]
    [InlineData("DAMAGE", "DPS")]
    [InlineData("anything else", "DPS")]
    [InlineData("", "DPS")]
    public async Task ListAsync_maps_blizzard_role_type_to_DPS_when_not_HEALER_or_TANK(
        string blizzardRoleType, string expectedRole)
    {
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<SpecializationIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SpecializationIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<PlayableSpecializationBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new PlayableSpecializationBlob(
                Id: 1, Name: "X",
                PlayableClass: new PlayableClassRefBlob(5),
                Role: new PlayableSpecRoleRefBlob(blizzardRoleType))));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAssetsBlob?)null);

        var repo = new SpecializationsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        Assert.Equal(expectedRole, Assert.Single(rows).Role);
    }

    [Fact]
    public async Task ListAsync_falls_back_and_defaults_classId_zero_when_playable_class_missing()
    {
        // Guards the `detail.PlayableClass?.Id ?? 0` path when a blob is
        // malformed (missing playable_class entirely).
        var blobs = new Mock<IBlobReferenceClient>();
        blobs.Setup(b => b.GetAsync<List<SpecializationIndexEntry>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SpecializationIndexEntry>?)null);
        blobs.Setup(b => b.ListAsync<PlayableSpecializationBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(AsAsync(new PlayableSpecializationBlob(
                Id: 99, Name: "Orphan", PlayableClass: null, Role: null)));
        blobs.Setup(b => b.GetAsync<MediaAssetsBlob>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAssetsBlob?)null);

        var repo = new SpecializationsRepository(blobs.Object);
        var rows = await repo.ListAsync(CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(0, row.ClassId);
        Assert.Equal("DPS", row.Role);
    }
}
