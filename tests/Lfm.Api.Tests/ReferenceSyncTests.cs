// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class ReferenceSyncTests
{
    // ── Fixture builders ─────────────────────────────────────────────────────

    private const string FakeToken = "fake-bnet-token";

    private static BlizzardJournalInstanceIndex MakeJournalInstanceIndex(params (int Id, string Name)[] entries) =>
        new(entries.Select(e => new BlizzardIndexEntry(e.Id, e.Name)).ToList());

    private static BlizzardJournalInstanceDetail MakeJournalInstanceDetail(
        int id,
        string name,
        string? expansionName = "The War Within",
        params (string ModeType, int Players)[] modes)
    {
        var modeRecords = modes
            .Select(m => new BlizzardJournalInstanceMode(
                Mode: new BlizzardJournalModeRef(m.ModeType, m.ModeType),
                Players: m.Players,
                IsTracked: true))
            .ToList();
        return new BlizzardJournalInstanceDetail(
            Id: id,
            Name: name,
            Category: new BlizzardJournalInstanceCategory("RAID"),
            Expansion: expansionName is null ? null : new BlizzardJournalExpansion(11, expansionName),
            MinimumLevel: 80,
            Modes: modeRecords,
            Media: null);
    }

    private static BlizzardPlayableSpecIndex MakeSpecIndex(params (int Id, string Name)[] entries) =>
        new(entries.Select(e => new BlizzardIndexEntry(e.Id, e.Name)).ToList());

    private static BlizzardPlayableSpecDetail MakeSpecDetail(
        int id,
        string name,
        int classId,
        string roleType) =>
        new(
            Id: id,
            Name: name,
            PlayableClass: new BlizzardPlayableSpecClassRef(classId, "Priest"),
            Role: new BlizzardPlayableSpecRoleRef(roleType, roleType));

    private static (
        ReferenceSync sut,
        Mock<IBlizzardGameDataClient> blizzard,
        Mock<IInstancesRepository> instances,
        Mock<ISpecializationsRepository> specs,
        TestLogger<ReferenceSync> logger) MakeSut()
    {
        var blizzard = new Mock<IBlizzardGameDataClient>();
        blizzard.Setup(b => b.GetClientCredentialsTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(FakeToken);
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceIndex());
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex());

        var instances = new Mock<IInstancesRepository>();
        instances.Setup(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<InstanceDocument>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var specs = new Mock<ISpecializationsRepository>();
        specs.Setup(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<SpecializationDocument>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<ReferenceSync>();
        var sut = new ReferenceSync(blizzard.Object, instances.Object, specs.Object, logger);

        return (sut, blizzard, instances, specs, logger);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAllAsync_syncs_instances_and_specializations_in_order()
    {
        var (sut, blizzard, instances, specs, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceIndex((10, "Liberation of Undermine")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceDetail(10, "Liberation of Undermine", modes: ("HEROIC", 25)));
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", classId: 5, roleType: "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(new[] { new BlizzardMediaAsset("icon", "https://render.example/icon.jpg") }));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("instances", response.Results[0].Name);
        Assert.StartsWith("synced", response.Results[0].Status);
        Assert.Equal("specializations", response.Results[1].Name);
        Assert.StartsWith("synced", response.Results[1].Status);

        instances.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<InstanceDocument>>(docs => docs.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        specs.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<SpecializationDocument>>(docs => docs.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_emits_one_document_per_mode_for_multi_mode_instance()
    {
        var (sut, blizzard, instances, _, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceIndex((10, "Multi")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(10, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceDetail(10, "Multi", modes: new[]
            {
                ("NORMAL", 25),
                ("HEROIC", 25),
                ("MYTHIC", 20),
            }));

        await sut.SyncAllAsync(CancellationToken.None);

        instances.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<InstanceDocument>>(docs => docs.Count() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_emits_unknown_mode_sentinel_when_modes_list_is_empty()
    {
        var (sut, blizzard, instances, _, _) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceIndex((42, "Solo Dungeon")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(42, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceDetail(42, "Solo Dungeon", modes: Array.Empty<(string, int)>()));

        await sut.SyncAllAsync(CancellationToken.None);

        instances.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<InstanceDocument>>(docs =>
                docs.Count() == 1
                && docs.Single().ModeKey == "UNKNOWN:0"
                && docs.Single().Id == "42:UNKNOWN:0"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Resilience ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAllAsync_records_failure_for_instances_but_still_syncs_specializations()
    {
        var (sut, blizzard, instances, specs, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("blizzard 503"));
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", 5, "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(null));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.Equal(2, response.Results.Count);
        Assert.Equal("instances", response.Results[0].Name);
        Assert.StartsWith("failed:", response.Results[0].Status);
        Assert.Equal("specializations", response.Results[1].Name);
        Assert.StartsWith("synced", response.Results[1].Status);

        instances.Verify(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<InstanceDocument>>(), It.IsAny<CancellationToken>()), Times.Never);
        specs.Verify(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<SpecializationDocument>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("instances"));
    }

    [Fact]
    public async Task SyncAllAsync_skips_instance_when_detail_fetch_fails_but_continues_index()
    {
        var (sut, blizzard, instances, _, logger) = MakeSut();
        blizzard.Setup(b => b.GetJournalInstanceIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceIndex((1, "Skipped"), (2, "Worked")));
        blizzard.Setup(b => b.GetJournalInstanceAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("404"));
        blizzard.Setup(b => b.GetJournalInstanceAsync(2, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeJournalInstanceDetail(2, "Worked", modes: ("HEROIC", 25)));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.StartsWith("synced", response.Results[0].Status);
        instances.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<InstanceDocument>>(docs => docs.Count() == 1 && docs.Single().Name == "Worked"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && (e.Message ?? "").Contains("Skipping"));
    }

    [Fact]
    public async Task SyncAllAsync_continues_when_spec_media_fetch_fails_with_null_icon_url()
    {
        var (sut, blizzard, _, specs, logger) = MakeSut();
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(257, "Holy", 5, "HEALER"));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("media 404"));

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.StartsWith("synced", response.Results[1].Status);
        specs.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<SpecializationDocument>>(docs =>
                docs.Single().IconUrl == null
                && docs.Single().Role == "HEALER"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    // ── Role mapping ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HEALER", "HEALER")]
    [InlineData("TANK", "TANK")]
    [InlineData("DAMAGE", "DPS")]
    [InlineData("anything else", "DPS")]
    [InlineData("", "DPS")]
    public async Task SyncAllAsync_maps_blizzard_role_type_to_DPS_when_not_HEALER_or_TANK(
        string blizzardRoleType, string expectedRole)
    {
        var (sut, blizzard, _, specs, _) = MakeSut();
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((1, "X")));
        blizzard.Setup(b => b.GetPlayableSpecAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecDetail(1, "X", 5, blizzardRoleType));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(1, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(null));

        await sut.SyncAllAsync(CancellationToken.None);

        specs.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<SpecializationDocument>>(docs => docs.Single().Role == expectedRole),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
