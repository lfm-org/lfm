// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Phase 3 writer coverage. <see cref="ReferenceSync"/> fetches the Blizzard
/// index + details + media, uploads per-id detail and media blobs to the wow
/// container, and emits <c>reference/{kind}/index.json</c> — the list-endpoint
/// manifest the repositories read as a single blob GET.
/// </summary>
public class ReferenceSyncTests
{
    private const string FakeToken = "fake-bnet-token";

    // ── Fixture builders ─────────────────────────────────────────────────────

    private static BlizzardJournalInstanceIndex MakeJournalIndex(params (int Id, string Name)[] entries) =>
        new(entries.Select(e => new BlizzardIndexEntry(e.Id, e.Name)).ToList());

    private static BlizzardJournalInstanceDetail MakeInstanceDetail(
        int id,
        string name,
        string? expansionName = "The War Within",
        params (string ModeType, int Players)[] modes) =>
        new(
            Id: id,
            Name: name,
            Category: new BlizzardJournalInstanceCategory("RAID"),
            Expansion: expansionName is null ? null : new BlizzardJournalExpansion(11, expansionName),
            MinimumLevel: 80,
            Modes: modes
                .Select(m => new BlizzardJournalInstanceMode(
                    Mode: new BlizzardJournalModeRef(m.ModeType, m.ModeType),
                    Players: m.Players,
                    IsTracked: true))
                .ToList(),
            Media: null);

    private static BlizzardPlayableSpecIndex MakeSpecIndex(params (int Id, string Name)[] entries) =>
        new(entries.Select(e => new BlizzardIndexEntry(e.Id, e.Name)).ToList());

    private static BlizzardJournalExpansionIndex MakeExpansionIndex(params (int Id, string Name)[] entries) =>
        new(entries.Select(e => new BlizzardIndexEntry(e.Id, e.Name)).ToList());

    private static BlizzardPlayableSpecDetail MakeSpecDetail(
        int id, string name, int classId, string roleType) =>
        new(
            Id: id,
            Name: name,
            PlayableClass: new BlizzardPlayableSpecClassRef(classId, "Priest"),
            Role: new BlizzardPlayableSpecRoleRef(roleType, roleType));

    private static (
        ReferenceSync sut,
        Mock<IBlizzardGameDataClient> blizzard,
        Mock<IBlobReferenceClient> blobs,
        TestLogger<ReferenceSync> logger) MakeSut()
    {
        var blizzard = new Mock<IBlizzardGameDataClient>();
        blizzard.Setup(b => b.GetClientCredentialsTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeToken);
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex());
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex());
        blizzard.Setup(b => b.GetJournalExpansionIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeExpansionIndex());

        var blobs = new Mock<IBlobReferenceClient>();
        var logger = new TestLogger<ReferenceSync>();
        var sut = new ReferenceSync(blizzard.Object, blobs.Object, logger);
        return (sut, blizzard, blobs, logger);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAllAsync_uploads_detail_media_and_manifest_for_each_entity()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((10, "Undermine")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstanceDetail(10, "Undermine", modes: ("HEROIC", 25)));
        blizzard.Setup(b => b.GetJournalInstanceMediaAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(new[]
            {
                new BlizzardMediaAsset("tile", "https://render.example/tile-10.jpg"),
            }));
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", classId: 5, roleType: "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(new[]
            {
                new BlizzardMediaAsset("icon", "https://render.example/icon-257.jpg"),
            }));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.StartsWith("synced", response.Results[0].Status);
        Assert.StartsWith("synced", response.Results[1].Status);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/10.json",
            It.Is<JournalInstanceBlob>(d => d.Id == 10 && d.Name == "Undermine"),
            It.IsAny<CancellationToken>()), Times.Once);
        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance-media/10.json",
            It.Is<MediaAssetsBlob>(m => m.Assets!.Single().Value == "https://render.example/tile-10.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);
        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix =>
                ix.Count == 1 &&
                ix[0].Id == 10 &&
                ix[0].Modes!.Single().ModeKey == "HEROIC:25" &&
                ix[0].PortraitUrl == "https://render.example/tile-10.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);

        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization/257.json",
            It.Is<PlayableSpecializationBlob>(d => d.Id == 257 && d.PlayableClass!.Id == 5),
            It.IsAny<CancellationToken>()), Times.Once);
        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization-media/257.json",
            It.IsAny<MediaAssetsBlob>(),
            It.IsAny<CancellationToken>()), Times.Once);
        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization/index.json",
            It.Is<List<SpecializationIndexEntry>>(ix =>
                ix.Count == 1 &&
                ix[0].Id == 257 &&
                ix[0].Role == "HEALER" &&
                ix[0].IconUrl == "https://render.example/icon-257.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncInstancesAsync_emits_unknown_sentinel_mode_when_detail_has_no_modes()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((42, "Solo")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(42, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstanceDetail(42, "Solo", modes: Array.Empty<(string, int)>()));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix =>
                ix.Single().Modes!.Single().ModeKey == "UNKNOWN:0"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncInstancesAsync_emits_one_mode_entry_per_blizzard_mode()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((10, "Multi")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstanceDetail(10, "Multi", modes: new[]
            {
                ("NORMAL", 25),
                ("HEROIC", 25),
                ("MYTHIC", 20),
            }));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix =>
                ix.Single().Modes!.Count == 3 &&
                ix.Single().Modes!.Any(m => m.ModeKey == "HEROIC:25")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Resilience ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAllAsync_records_failure_for_instances_but_still_syncs_specializations()
    {
        var (sut, blizzard, blobs, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blizzard 503"));
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", 5, "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(null));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.StartsWith("failed:", response.Results[0].Status);
        Assert.StartsWith("synced", response.Results[1].Status);
        blobs.Verify(b => b.UploadAsync(
            It.Is<string>(n => n.StartsWith("reference/journal-instance/")),
            It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization/index.json",
            It.IsAny<List<SpecializationIndexEntry>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("instances"));
    }

    [Fact]
    public async Task SyncInstancesAsync_skips_instance_when_detail_fetch_fails_but_continues_index()
    {
        var (sut, blizzard, blobs, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((1, "Skipped"), (2, "Worked")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404"));
        blizzard.Setup(b => b.GetJournalInstanceAsync(2, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstanceDetail(2, "Worked", modes: ("HEROIC", 25)));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix => ix.Single().Id == 2 && ix.Single().Name == "Worked"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && (e.Message ?? "").Contains("Skipping"));
    }

    [Fact]
    public async Task SyncInstancesAsync_continues_when_media_fetch_fails_with_null_portrait_url()
    {
        var (sut, blizzard, blobs, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((10, "Undermine")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstanceDetail(10, "Undermine", modes: ("NORMAL", 25)));
        blizzard.Setup(b => b.GetJournalInstanceMediaAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("media 404"));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance-media/10.json",
            It.IsAny<MediaAssetsBlob>(),
            It.IsAny<CancellationToken>()), Times.Never);
        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix => ix.Single().PortraitUrl == null),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SyncSpecializationsAsync_continues_when_spec_media_fetch_fails_with_null_icon_url()
    {
        var (sut, blizzard, blobs, logger) = MakeSut();
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", 5, "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("media 404"));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization/index.json",
            It.Is<List<SpecializationIndexEntry>>(ix => ix.Single().IconUrl == null && ix.Single().Role == "HEALER"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SyncInstancesAsync_retries_after_429_and_still_writes_manifest_entry()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalIndex((10, "Retrying")));
        var callCount = 0;
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
                return MakeInstanceDetail(10, "Retrying", modes: ("HEROIC", 25));
            });

        await sut.SyncAllAsync(CancellationToken.None);

        Assert.Equal(2, callCount);
        blobs.Verify(b => b.UploadAsync(
            "reference/journal-instance/index.json",
            It.Is<List<InstanceIndexEntry>>(ix => ix.Single().Id == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Role mapping ────────────────────────────────────────────────────────

    // ── Expansion sync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SyncExpansionsAsync_uploads_manifest_in_blizzard_order()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalExpansionIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeExpansionIndex(
                (68, "Classic"),
                (67, "The Burning Crusade"),
                (505, "The War Within")));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        var expansionsResult = response.Results.Single(r => r.Name == "expansions");
        Assert.Equal("synced (3 docs)", expansionsResult.Status);

        blobs.Verify(b => b.UploadAsync(
            "reference/journal-expansion/index.json",
            It.Is<List<ExpansionIndexEntry>>(ix =>
                ix.Count == 3 &&
                ix[0].Id == 68 && ix[0].Name == "Classic" &&
                ix[2].Id == 505 && ix[2].Name == "The War Within"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncExpansionsAsync_writes_empty_manifest_when_index_empty()
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        // default setup already returns an empty expansion index

        var response = await sut.SyncAllAsync(CancellationToken.None);

        var expansionsResult = response.Results.Single(r => r.Name == "expansions");
        Assert.Equal("synced (0 docs)", expansionsResult.Status);
        blobs.Verify(b => b.UploadAsync(
            "reference/journal-expansion/index.json",
            It.Is<List<ExpansionIndexEntry>>(ix => ix.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_records_failure_for_expansions_but_still_syncs_others()
    {
        var (sut, blizzard, _, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalExpansionIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blizzard 503"));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        var expansionsResult = response.Results.Single(r => r.Name == "expansions");
        Assert.StartsWith("failed:", expansionsResult.Status);
        // instances + specializations should still have succeeded (empty indexes)
        Assert.StartsWith("synced", response.Results.Single(r => r.Name == "instances").Status);
        Assert.StartsWith("synced", response.Results.Single(r => r.Name == "specializations").Status);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("expansions"));
    }

    // ── Role mapping ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HEALER", "HEALER")]
    [InlineData("TANK", "TANK")]
    [InlineData("DAMAGE", "DPS")]
    [InlineData("anything else", "DPS")]
    [InlineData("", "DPS")]
    public async Task SyncSpecializationsAsync_maps_blizzard_role_type_to_DPS_when_not_HEALER_or_TANK(
        string blizzardRoleType, string expectedRole)
    {
        var (sut, blizzard, blobs, _) = MakeSut();
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((1, "X")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(1, "X", 5, blizzardRoleType));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(null));

        await sut.SyncAllAsync(CancellationToken.None);

        blobs.Verify(b => b.UploadAsync(
            "reference/playable-specialization/index.json",
            It.Is<List<SpecializationIndexEntry>>(ix => ix.Single().Role == expectedRole),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
