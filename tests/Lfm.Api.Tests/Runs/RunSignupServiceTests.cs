// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Runs;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Runs;

/// <summary>
/// Unit tests for <see cref="RunSignupService"/>. Each test exercises one
/// branch of the policy lifted out of <c>RunsSignupFunction.Run</c>; the
/// function-level tests in <see cref="RunsSignupFunctionTests"/> stay as
/// the HTTP-shaped integration coverage on top of this service.
/// </summary>
public class RunSignupServiceTests
{
    // Anchored to UtcNow so the fixtures never become time bombs against a
    // future-dated assertion.
    private static string FutureStartTime() =>
        DateTimeOffset.UtcNow.AddHours(24).ToString("o");
    private static string FutureSignupCloseTime() =>
        DateTimeOffset.UtcNow.AddHours(22).ToString("o");

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-user") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "User#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaider(
        string battleNetId = "bnet-user",
        string characterId = "char-1",
        int? guildId = null,
        StoredSpecializationsSummary? specializationsSummary = null) =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: characterId,
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: characterId,
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Testchar",
                    GuildId: guildId,
                    GuildName: guildId is not null ? "Test Guild" : null,
                    SpecializationsSummary: specializationsSummary)
            ]);

    private static RunDocument MakeOpenRun(
        string id = "run-1",
        string visibility = "PUBLIC",
        int? creatorGuildId = null,
        IReadOnlyList<RunCharacterEntry>? runCharacters = null,
        IReadOnlyList<string>? rejected = null) =>
        new RunDocument(
            Id: id,
            StartTime: FutureStartTime(),
            SignupCloseTime: FutureSignupCloseTime(),
            Description: "Test run",
            ModeKey: "NORMAL:10",
            Visibility: visibility,
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: runCharacters ?? [],
            Difficulty: "NORMAL",
            Size: 10,
            RejectedRaiderBattleNetIds: rejected);

    private static SignupRequest MakeBody(
        string characterId = "char-1",
        string desiredAttendance = "IN",
        int? specId = null) =>
        new SignupRequest(
            CharacterId: characterId,
            DesiredAttendance: desiredAttendance,
            SpecId: specId);

    private static (
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildPermissions> guildPermissions,
        Mock<ILogger<RunSignupService>> logger,
        RunSignupService sut) MakeSut()
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        var logger = new Mock<ILogger<RunSignupService>>();
        var sut = new RunSignupService(
            runsRepo.Object,
            raidersRepo.Object,
            guildPermissions.Object,
            logger.Object);
        return (runsRepo, raidersRepo, guildPermissions, logger, sut);
    }

    [Fact]
    public async Task SignupAsync_RaiderMissing_ReturnsNotFound()
    {
        var (_, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var notFound = Assert.IsType<RunOperationResult.NotFound>(result);
        Assert.Equal("raider-not-found", notFound.Code);
    }

    [Fact]
    public async Task SignupAsync_CharacterNotOnProfile_ReturnsBadRequest()
    {
        var (_, raidersRepo, _, _, sut) = MakeSut();
        // Raider has only "char-1"; body asks for "char-missing".
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-missing"),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var badRequest = Assert.IsType<RunOperationResult.BadRequest>(result);
        Assert.Equal("character-not-on-profile", badRequest.Code);
    }

    [Fact]
    public async Task SignupAsync_RunNotFound_ReturnsNotFound()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("missing-run", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var result = await sut.SignupAsync(
            "missing-run",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var notFound = Assert.IsType<RunOperationResult.NotFound>(result);
        Assert.Equal("run-not-found", notFound.Code);
    }

    [Fact]
    public async Task SignupAsync_SignupsClosed_ReturnsConflict()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        // Run whose start time and signup close time are both in the past.
        var closed = MakeOpenRun() with
        {
            StartTime = DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),
            SignupCloseTime = DateTimeOffset.UtcNow.AddHours(-2).ToString("o"),
        };
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(closed);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var conflict = Assert.IsType<RunOperationResult.ConflictResult>(result);
        Assert.Equal("signups-closed", conflict.Code);
    }

    [Fact]
    public async Task SignupAsync_HappyPath_PublicRun_ReturnsOkWithEntry()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun());
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, string? _, CancellationToken _) => doc);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-1", desiredAttendance: "IN"),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        var entry = Assert.Single(ok.Run.RunCharacters);
        Assert.Equal("bnet-user", entry.RaiderBattleNetId);
        Assert.Equal("char-1", entry.CharacterId);
        Assert.Equal("IN", entry.DesiredAttendance);
        Assert.Equal("IN", entry.ReviewedAttendance);
        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignupAsync_RetryExhaustion_ReturnsConflict()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun());
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var conflict = Assert.IsType<RunOperationResult.ConflictResult>(result);
        Assert.Equal("concurrent-modification", conflict.Code);
        runsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ------------------------------------------------------------------
    // GUILD visibility — non-member must see NotFound (don't leak existence).
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_GuildVisibility_NonMember_ReturnsNotFound_DoesNotLeakExistence()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        // Caller's selected character is in guild 999; run is owned by guild 123.
        // RunAccessPolicy.CanView returns false, so the policy lifts the run as
        // "not found" rather than "forbidden" so existence cannot be probed.
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1", guildId: 999));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun(visibility: "GUILD", creatorGuildId: 123));

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var notFound = Assert.IsType<RunOperationResult.NotFound>(result);
        Assert.Equal("run-not-found", notFound.Code);
        // No write should happen on the not-found branch.
        runsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // GUILD visibility — same-guild member without rank permission gets
    // Forbidden + the audit-reason literal the function relies on.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_GuildVisibility_MemberWithoutPermission_ReturnsForbiddenWithAuditReason()
    {
        var (runsRepo, raidersRepo, guildPermissions, _, sut) = MakeSut();
        // Same guild as the run (CanView true), but rank lacks canSignupGuildRuns.
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1", guildId: 123));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun(visibility: "GUILD", creatorGuildId: 123));
        guildPermissions
            .Setup(g => g.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var forbidden = Assert.IsType<RunOperationResult.Forbidden>(result);
        Assert.Equal("guild-rank-denied", forbidden.Code);
        Assert.Equal("guild rank denied", forbidden.AuditReason);
        runsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // GUILD visibility — happy path: same guild + rank permission allows
    // the signup to land.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_GuildVisibility_MemberWithPermission_ReturnsOk()
    {
        var (runsRepo, raidersRepo, guildPermissions, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1", guildId: 123));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun(visibility: "GUILD", creatorGuildId: 123));
        guildPermissions
            .Setup(g => g.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, string? _, CancellationToken _) => doc);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-1", desiredAttendance: "IN"),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        var entry = Assert.Single(ok.Run.RunCharacters);
        Assert.Equal("bnet-user", entry.RaiderBattleNetId);
        Assert.Equal("char-1", entry.CharacterId);
        Assert.Equal("IN", entry.DesiredAttendance);
        Assert.Equal("IN", entry.ReviewedAttendance);
    }

    // ------------------------------------------------------------------
    // Rejection list — a NEW signup (no prior entry) by a raider whose
    // BattleNetId sits in RejectedRaiderBattleNetIds defaults to OUT, the
    // structural defence against the cancel-then-resignup bypass.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_RaiderInRejectionList_NewSignup_DefaultsToOut()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun(rejected: ["bnet-user"]));
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, string? _, CancellationToken _) => doc);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-1", desiredAttendance: "IN"),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        var entry = Assert.Single(ok.Run.RunCharacters);
        Assert.Equal("bnet-user", entry.RaiderBattleNetId);
        Assert.Equal("IN", entry.DesiredAttendance);
        // The rejection-list flip turns the *new* signup's review status to OUT.
        Assert.Equal("OUT", entry.ReviewedAttendance);
    }

    // ------------------------------------------------------------------
    // Rejection list — an EDIT to an existing entry preserves the prior
    // ReviewedAttendance. The flip-to-OUT only applies to brand-new signups.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_RaiderInRejectionList_EditExistingEntry_PreservesPriorReviewedAttendance()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        var existingEntry = new RunCharacterEntry(
            Id: "entry-1",
            CharacterId: "char-1",
            CharacterName: "Testchar",
            CharacterRealm: "silvermoon",
            CharacterLevel: 80,
            CharacterClassId: 1,
            CharacterClassName: "Warrior",
            CharacterRaceId: 0,
            CharacterRaceName: "",
            RaiderBattleNetId: "bnet-user",
            DesiredAttendance: "IN",
            ReviewedAttendance: "BENCH",
            SpecId: null,
            SpecName: null,
            Role: null);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun(
                runCharacters: [existingEntry],
                rejected: ["bnet-user"]));
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, string? _, CancellationToken _) => doc);

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-1", desiredAttendance: "IN"),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        var entry = Assert.Single(ok.Run.RunCharacters);
        // Same entry id (upsert, not insert).
        Assert.Equal("entry-1", entry.Id);
        // Edits preserve the run owner's prior review decision; the OUT flip
        // only applies to brand-new signups for raiders in the rejection list.
        Assert.Equal("BENCH", entry.ReviewedAttendance);
    }

    // ------------------------------------------------------------------
    // Concurrency — a single ConcurrencyConflictException retries; on the
    // second attempt the write succeeds and the run is re-read each time.
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_RetryConcurrencyConflict_ThenSucceeds_ReturnsOk()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider("bnet-user", characterId: "char-1"));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun());

        // First UpdateAsync call throws, second succeeds.
        var updateCall = 0;
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<RunDocument, string?, CancellationToken>((doc, _, _) =>
            {
                updateCall++;
                if (updateCall == 1)
                    throw new ConcurrencyConflictException();
                return Task.FromResult(doc);
            });

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        Assert.IsType<RunOperationResult.Ok>(result);
        // Two attempts → two writes attempted, two re-reads.
        runsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        runsRepo.Verify(
            r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ------------------------------------------------------------------
    // Spec validation — body's SpecId must exist on the character's
    // SpecializationsSummary; otherwise BadRequest("invalid-spec-id").
    // ------------------------------------------------------------------
    [Fact]
    public async Task SignupAsync_InvalidSpecId_ReturnsBadRequest()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        // Character knows spec 71 only; body asks for spec 999.
        var specs = new StoredSpecializationsSummary(
            ActiveSpecialization: null,
            Specializations: [
                new StoredSpecializationsEntry(
                    new StoredCharacterSpecialization(Id: 71, Name: "Arms")),
            ]);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider(
                "bnet-user",
                characterId: "char-1",
                specializationsSummary: specs));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeOpenRun());

        var result = await sut.SignupAsync(
            "run-1",
            MakeBody(characterId: "char-1", specId: 999),
            MakePrincipal("bnet-user"),
            CancellationToken.None);

        var badRequest = Assert.IsType<RunOperationResult.BadRequest>(result);
        Assert.Equal("invalid-spec-id", badRequest.Code);
        runsRepo.Verify(
            r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
