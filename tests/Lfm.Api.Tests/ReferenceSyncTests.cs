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
        Mock<ISpecializationsRepository> specs,
        TestLogger<ReferenceSync> logger) MakeSut()
    {
        var blizzard = new Mock<IBlizzardGameDataClient>();
        blizzard.Setup(b => b.GetClientCredentialsTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(FakeToken);
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex());

        var specs = new Mock<ISpecializationsRepository>();
        specs.Setup(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<SpecializationDocument>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<ReferenceSync>();
        var sut = new ReferenceSync(blizzard.Object, specs.Object, logger);

        return (sut, blizzard, specs, logger);
    }

    // ── Instance sync: stubbed (Phase 1 moved reads to blob; Phase 3 rewrites the writer) ─

    [Fact]
    public async Task SyncAllAsync_reports_instances_as_failed_pending_phase_3_blob_writer()
    {
        // Phase 1: SyncInstancesAsync throws NotImplementedException so the admin
        // endpoint surfaces a clear "failed: ..." message, while the reader in
        // InstancesRepository continues to serve the existing blob data. Spec sync
        // (still Cosmos-backed in commit B) runs to completion.
        var (sut, blizzard, specs, logger) = MakeSut();
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
        Assert.Contains("Phase 3", response.Results[0].Status);
        Assert.Equal("specializations", response.Results[1].Name);
        Assert.StartsWith("synced", response.Results[1].Status);

        specs.Verify(r => r.UpsertBatchAsync(It.IsAny<IEnumerable<SpecializationDocument>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("instances"));
    }

    // ── Spec sync: still Cosmos-backed in commit B ───────────────────────────

    [Fact]
    public async Task SyncAllAsync_continues_when_spec_media_fetch_fails_with_null_icon_url()
    {
        var (sut, blizzard, specs, logger) = MakeSut();
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

    [Fact]
    public async Task SyncAllAsync_retries_specialization_after_429_and_still_writes_document()
    {
        var (sut, blizzard, specs, _) = MakeSut();
        blizzard.Setup(b => b.GetPlayableSpecIndexAsync(FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecIndex((257, "Holy")));
        blizzard.Setup(b => b.GetPlayableSpecMediaAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlizzardMediaAssets(null));

        var callCount = 0;
        blizzard
            .Setup(b => b.GetPlayableSpecAsync(257, FakeToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new HttpRequestException("rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
                return MakeSpecDetail(257, "Holy", classId: 5, roleType: "HEALER");
            });

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.StartsWith("synced", response.Results[1].Status);
        Assert.Equal(2, callCount);
        specs.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<SpecializationDocument>>(docs => docs.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
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
        var (sut, blizzard, specs, _) = MakeSut();
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
