// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Runs;
using Lfm.Api.Services;
using Lfm.Contracts.Instances;
using Lfm.Contracts.Runs;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Runs;

/// <summary>
/// Unit tests for <see cref="RunUpdateService"/>. Each test exercises one
/// branch of the policy lifted out of <c>RunsUpdateFunction.Run</c>; the
/// function-level tests in <see cref="RunsUpdateFunctionTests"/> stay as the
/// HTTP-shaped integration coverage on top of this service.
/// </summary>
public class RunUpdateServiceTests
{
    private const string IfMatchEtag = "\"test-etag\"";

    // Anchored to UtcNow so the fixtures never become time bombs against a
    // future-dated assertion.
    private static string FutureStartTime() =>
        DateTimeOffset.UtcNow.AddHours(24).ToString("o");
    private static string FutureSignupCloseTime() =>
        DateTimeOffset.UtcNow.AddHours(22).ToString("o");

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-creator") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Creator#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaider(
        string battleNetId = "bnet-creator",
        int? guildId = 12345,
        string? guildName = "Test Guild") =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Testchar",
                    GuildId: guildId,
                    GuildName: guildName)
            ]);

    private static RunDocument MakeOpenRun(
        string id = "run-1",
        string creatorBattleNetId = "bnet-creator",
        int? creatorGuildId = 12345,
        string visibility = "GUILD",
        IReadOnlyList<RunCharacterEntry>? runCharacters = null) =>
        new RunDocument(
            Id: id,
            StartTime: FutureStartTime(),
            SignupCloseTime: FutureSignupCloseTime(),
            Description: "Original description",
            ModeKey: "NORMAL:10",
            Visibility: visibility,
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: creatorBattleNetId,
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: runCharacters ?? [],
            Difficulty: "NORMAL",
            Size: 10);

    private static UpdateRunRequest MakeBody(
        string? description = null,
        string? difficulty = null,
        int? size = null) =>
        new UpdateRunRequest(
            StartTime: null,
            SignupCloseTime: null,
            Description: description,
            Visibility: null,
            InstanceId: null,
            InstanceName: null,
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: null);

    private static RunUpdatePresentFields MakePresent(
        bool description = false,
        bool difficulty = false,
        bool size = false) =>
        new RunUpdatePresentFields(
            StartTime: false,
            SignupCloseTime: false,
            Description: description,
            Visibility: false,
            InstanceId: false,
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: false);

    private static (
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildPermissions> guildPermissions,
        Mock<IInstancesRepository> instancesRepo,
        RunUpdateService sut) MakeSut()
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();
        var sut = new RunUpdateService(
            runsRepo.Object,
            raidersRepo.Object,
            guildPermissions.Object,
            instancesRepo.Object);
        return (runsRepo, raidersRepo, guildPermissions, instancesRepo, sut);
    }

    [Fact]
    public async Task UpdateAsync_RunNotFound_ReturnsNotFound()
    {
        var (runsRepo, _, _, _, sut) = MakeSut();
        runsRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var result = await sut.UpdateAsync(
            "missing",
            MakeBody(description: "x"),
            MakePresent(description: true),
            IfMatchEtag,
            MakePrincipal(),
            CancellationToken.None);

        var notFound = Assert.IsType<RunOperationResult.NotFound>(result);
        Assert.Equal("run-not-found", notFound.Code);
    }

    [Fact]
    public async Task UpdateAsync_NotCreator_NotGuildPeer_ReturnsForbidden()
    {
        // Caller belongs to guild 99999 — different from the run's creator guild (12345),
        // and is not the creator.
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        var existing = MakeOpenRun(creatorBattleNetId: "bnet-creator", creatorGuildId: 12345);
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-other", guildId: 99999));

        var result = await sut.UpdateAsync(
            "run-1",
            MakeBody(description: "x"),
            MakePresent(description: true),
            IfMatchEtag,
            MakePrincipal("bnet-other"),
            CancellationToken.None);

        var forbidden = Assert.IsType<RunOperationResult.Forbidden>(result);
        Assert.Equal("run-update-not-creator", forbidden.Code);
        Assert.Equal("not creator", forbidden.AuditReason);
    }

    [Fact]
    public async Task UpdateAsync_EditingClosed_ReturnsConflict()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        // Run whose startTime is in the past → editing closed.
        var pastRun = MakeOpenRun() with
        {
            StartTime = DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),
            SignupCloseTime = DateTimeOffset.UtcNow.AddHours(-2).ToString("o"),
        };
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastRun);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-creator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-creator"));

        var result = await sut.UpdateAsync(
            "run-1",
            MakeBody(description: "Too late"),
            MakePresent(description: true),
            IfMatchEtag,
            MakePrincipal("bnet-creator"),
            CancellationToken.None);

        var conflict = Assert.IsType<RunOperationResult.ConflictResult>(result);
        Assert.Equal("run-editing-closed", conflict.Code);
    }

    [Fact]
    public async Task UpdateAsync_StaleEtag_ReturnsPreconditionFailed()
    {
        var (runsRepo, raidersRepo, _, instancesRepo, sut) = MakeSut();
        var existing = MakeOpenRun(creatorBattleNetId: "bnet-creator");
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-creator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-creator"));
        instancesRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("631:NORMAL:10", 631, "Icecrown Citadel", "NORMAL:10", "wrath",
                    Difficulty: "NORMAL", Size: 10),
            });

        var result = await sut.UpdateAsync(
            "run-1",
            MakeBody(description: "Updated"),
            MakePresent(description: true),
            "\"stale-etag\"",
            MakePrincipal("bnet-creator"),
            CancellationToken.None);

        var pf = Assert.IsType<RunOperationResult.PreconditionFailed>(result);
        Assert.Equal("if-match-stale", pf.Code);
    }

    [Fact]
    public async Task UpdateAsync_HappyPath_ReturnsOkWithUpdatedDocument()
    {
        var (runsRepo, raidersRepo, _, instancesRepo, sut) = MakeSut();
        var existing = MakeOpenRun(creatorBattleNetId: "bnet-creator");
        RunDocument? captured = null;
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<RunDocument, string?, CancellationToken>((doc, _, _) => captured = doc)
            .ReturnsAsync((RunDocument doc, string? _, CancellationToken _) => doc);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-creator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-creator"));
        instancesRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>
            {
                new("631:HEROIC:25", 631, "Icecrown Citadel", "HEROIC:25", "wrath",
                    Difficulty: "HEROIC", Size: 25),
            });

        var body = MakeBody(description: "Updated description", difficulty: "HEROIC", size: 25);
        var present = MakePresent(description: true, difficulty: true, size: true);

        var result = await sut.UpdateAsync(
            "run-1",
            body,
            present,
            IfMatchEtag,
            MakePrincipal("bnet-creator"),
            CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        Assert.Equal("Updated description", ok.Run.Description);
        Assert.Equal("HEROIC", ok.Run.Difficulty);
        Assert.Equal(25, ok.Run.Size);
        Assert.Equal("HEROIC:25", ok.Run.ModeKey);
        Assert.NotNull(captured);
        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), IfMatchEtag, It.IsAny<CancellationToken>()), Times.Once);
    }
}
