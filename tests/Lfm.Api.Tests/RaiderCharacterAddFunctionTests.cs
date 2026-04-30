// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Services.Blizzard.Models;
using Lfm.Contracts.Raiders;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="RaiderCharacterAddFunction"/>.
///
/// Covers the POST /api/raider/character endpoint:
///  - Happy path (fresh fetch)
///  - Cache hit (fresh) vs cache miss (stale)
///  - Ownership checks (with and without account profile)
///  - Access token missing
///  - Blizzard fetch failure
///  - Validator errors
///  - Raider not found
/// </summary>
public class RaiderCharacterAddFunctionTests
{
    private const string FakeBattleNetId = "bnet-42";
    private const string FakeAccessToken = "bnet-access-token-xyz";
    private const string Region = "eu";

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal FakePrincipal(string? accessToken = FakeAccessToken) => new(
        BattleNetId: FakeBattleNetId,
        BattleTag: "Player#1234",
        GuildId: null,
        GuildName: null,
        IssuedAt: DateTimeOffset.UtcNow,
        ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
        AccessToken: accessToken);

    private static HttpRequest MakePostRequest(object body)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = "application/json";
        var json = JsonSerializer.Serialize(body);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentLength = httpContext.Request.Body.Length;
        return httpContext.Request;
    }

    private static RaiderCharacterAddFunction MakeFunction(
        Mock<IRaidersRepository> repoMock,
        Mock<IBlizzardProfileClient> profileMock) =>
        new(repoMock.Object, profileMock.Object, NullLogger<RaiderCharacterAddFunction>.Instance);

    private static StoredBlizzardAccountProfile MakeAccountProfileWithCharacter(string name, string realmSlug) =>
        new StoredBlizzardAccountProfile(
            WowAccounts:
            [
                new StoredBlizzardWowAccount(
                    Id: 1,
                    Characters:
                    [
                        new StoredBlizzardAccountCharacter(
                            Name: name,
                            Level: 80,
                            Realm: new StoredBlizzardRealmRef(Slug: realmSlug, Name: "Silvermoon"),
                            PlayableClass: new StoredBlizzardNamedRef(Id: 2, Name: "Paladin"))
                    ])
            ]);

    private static CharacterProfileResponse MakeProfileResponse(
        string name = "Aelrin",
        int level = 80,
        int classId = 2,
        string className = "Paladin",
        int? guildId = 42,
        string? guildName = "Midnight") =>
        new CharacterProfileResponse(
            Name: name,
            Level: level,
            CharacterClass: new NamedRefResponse(Id: classId, Name: className),
            Race: new NamedRefResponse(Id: 1, Name: "Human"),
            Realm: new RealmRefResponse(Slug: "silvermoon", Name: "Silvermoon"),
            Guild: guildId is null ? null : new CharacterGuildRefResponse(Id: guildId.Value, Name: guildName));

    private static CharacterSpecializationsResponse MakeSpecsResponse(int activeId = 65, string activeName = "Holy") =>
        new CharacterSpecializationsResponse(
            ActiveSpecialization: new SpecializationRefResponse(Id: activeId, Name: activeName),
            Specializations:
            [
                new SpecializationEntryResponse(new SpecializationRefResponse(Id: activeId, Name: activeName)),
                new SpecializationEntryResponse(new SpecializationRefResponse(Id: 66, Name: "Protection")),
                new SpecializationEntryResponse(new SpecializationRefResponse(Id: 70, Name: "Retribution")),
            ]);

    private static CharacterMediaSummaryResponse MakeMediaSummary(string avatar = "https://render.worldofwarcraft.com/avatar.jpg") =>
        new CharacterMediaSummaryResponse(
            Assets:
            [
                new CharacterMediaAssetResponse(Key: "avatar", Value: avatar),
                new CharacterMediaAssetResponse(Key: "main", Value: "https://render.worldofwarcraft.com/main.jpg"),
            ]);

    // -------------------------------------------------------------------------
    // Happy path — fresh character
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_character_dto_when_fresh_fetch_succeeds_with_owned_character()
    {
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: MakeAccountProfileWithCharacter("Aelrin", "silvermoon"));

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient
            .Setup(p => p.GetCharacterProfileAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProfileResponse());
        profileClient
            .Setup(p => p.GetCharacterSpecializationsAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecsResponse());
        profileClient
            .Setup(p => p.GetCharacterMediaAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMediaSummary());

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AddCharacterResponse>(ok.Value);
        Assert.Equal("eu-silvermoon-aelrin", response.SelectedCharacterId);
        Assert.Equal("Aelrin", response.Character.Name);
        Assert.Equal("silvermoon", response.Character.Realm);
        Assert.Equal(2, response.Character.ClassId);
        Assert.Equal("Paladin", response.Character.ClassName);
        Assert.Equal(80, response.Character.Level);
        Assert.Equal(65, response.Character.ActiveSpecId);
        Assert.Equal("Holy", response.Character.SpecName);
        Assert.Equal("https://render.worldofwarcraft.com/avatar.jpg", response.Character.PortraitUrl);

        // Upsert called once with selected character updated, the stored character present,
        // and the portrait cache populated.
        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d =>
                d.SelectedCharacterId == "eu-silvermoon-aelrin" &&
                d.Characters != null &&
                d.Characters.Any(c => c.Id == "eu-silvermoon-aelrin" && c.GuildId == 42 && c.GuildName == "Midnight") &&
                d.PortraitCache != null &&
                d.PortraitCache["eu-silvermoon-aelrin"] == "https://render.worldofwarcraft.com/avatar.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Cache hit — character is already stored and fresh (< 15 min)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Skips_blizzard_calls_when_cached_character_is_fresh()
    {
        var fetchedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        var cached = new StoredSelectedCharacter(
            Id: "eu-silvermoon-aelrin",
            Region: "eu",
            Realm: "silvermoon",
            Name: "Aelrin",
            PortraitUrl: "https://render.worldofwarcraft.com/cached.jpg",
            SpecializationsSummary: new StoredSpecializationsSummary(
                ActiveSpecialization: new StoredCharacterSpecialization(65, "Holy"),
                Specializations: [new StoredSpecializationsEntry(new StoredCharacterSpecialization(65, "Holy"))]),
            ClassId: 2,
            ClassName: "Paladin",
            Level: 80,
            GuildId: 42,
            GuildName: "Midnight",
            FetchedAt: fetchedAt);

        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: MakeAccountProfileWithCharacter("Aelrin", "silvermoon"),
            Characters: [cached]);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AddCharacterResponse>(ok.Value);
        Assert.Equal("eu-silvermoon-aelrin", response.SelectedCharacterId);
        Assert.Equal("https://render.worldofwarcraft.com/cached.jpg", response.Character.PortraitUrl);

        // Blizzard calls were skipped.
        profileClient.Verify(p => p.GetCharacterProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        profileClient.Verify(p => p.GetCharacterSpecializationsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        profileClient.Verify(p => p.GetCharacterMediaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // Cache stale — character exists but fetchedAt > 15 min ago → refetch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Fetches_fresh_data_when_cached_character_is_stale()
    {
        // 2 hours old — exceeds all tier TTLs (profile 1 h, specs 15 min, media 24 h... except media).
        // Use 25 hours to exceed even the 24-hour media TTL so all three tiers are stale.
        var staleFetchedAt = DateTimeOffset.UtcNow.AddHours(-25).ToString("O");
        var stale = new StoredSelectedCharacter(
            Id: "eu-silvermoon-aelrin",
            Region: "eu",
            Realm: "silvermoon",
            Name: "Aelrin",
            SpecializationsSummary: new StoredSpecializationsSummary(),
            FetchedAt: staleFetchedAt);

        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: MakeAccountProfileWithCharacter("Aelrin", "silvermoon"),
            Characters: [stale]);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient
            .Setup(p => p.GetCharacterProfileAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProfileResponse());
        profileClient
            .Setup(p => p.GetCharacterSpecializationsAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecsResponse());
        profileClient
            .Setup(p => p.GetCharacterMediaAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMediaSummary());

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        profileClient.Verify(p => p.GetCharacterProfileAsync(
            "silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Ownership — account profile present but character not in it → 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_403_when_character_not_in_account_profile()
    {
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            // Account profile exists but only contains a different character.
            AccountProfileSummary: MakeAccountProfileWithCharacter("Someoneelse", "silvermoon"));

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        repo.Verify(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        profileClient.Verify(p => p.GetCharacterProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Ownership — hyphenated realm slug must not false-positive
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regression test: the old suffix-match check would allow "eu-xx-mg-aelrin"
    /// (character on realm "xx-mg") to pass when the account only contains
    /// "aelrin" on realm "mg" — because "eu-xx-mg-aelrin".EndsWith("-mg-aelrin").
    /// The exact-match fix must reject this.
    /// </summary>
    [Fact]
    public void IsCharacterOwnedByAccount_does_not_false_positive_on_hyphenated_realm()
    {
        // Account contains aelrin on realm "mg".
        var profile = new StoredBlizzardAccountProfile(
            WowAccounts:
            [
                new StoredBlizzardWowAccount(
                    Id: 1,
                    Characters:
                    [
                        new StoredBlizzardAccountCharacter(
                            Name: "Aelrin",
                            Level: 80,
                            Realm: new StoredBlizzardRealmRef(Slug: "mg", Name: "Moonglade"),
                            PlayableClass: new StoredBlizzardNamedRef(Id: 2, Name: "Paladin"))
                    ])
            ]);

        // The requested character is on realm "xx-mg" — a different realm whose slug
        // ends with "-mg". Old suffix match would have returned true.
        var result = RaiderCharacterAddFunction.IsCharacterOwnedByAccount(
            characterId: "eu-xx-mg-aelrin",
            region: "eu",
            accountProfileSummary: profile);

        Assert.False(result);
    }

    [Fact]
    public void IsCharacterOwnedByAccount_returns_true_for_exact_match()
    {
        var profile = new StoredBlizzardAccountProfile(
            WowAccounts:
            [
                new StoredBlizzardWowAccount(
                    Id: 1,
                    Characters:
                    [
                        new StoredBlizzardAccountCharacter(
                            Name: "Aelrin",
                            Level: 80,
                            Realm: new StoredBlizzardRealmRef(Slug: "silvermoon", Name: "Silvermoon"),
                            PlayableClass: new StoredBlizzardNamedRef(Id: 2, Name: "Paladin"))
                    ])
            ]);

        var result = RaiderCharacterAddFunction.IsCharacterOwnedByAccount(
            characterId: "eu-silvermoon-aelrin",
            region: "eu",
            accountProfileSummary: profile);

        Assert.True(result);
    }

    // -------------------------------------------------------------------------
    // Ownership — no account profile → allowed (new raider flow)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Allows_add_when_account_profile_is_null()
    {
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: null); // new raider — hasn't synced yet

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient
            .Setup(p => p.GetCharacterProfileAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProfileResponse());
        profileClient
            .Setup(p => p.GetCharacterSpecializationsAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSpecsResponse());
        profileClient
            .Setup(p => p.GetCharacterMediaAsync("silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CharacterMediaSummaryResponse?)null);

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        profileClient.Verify(p => p.GetCharacterProfileAsync(
            "silvermoon", "aelrin", FakeAccessToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Missing access token → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_401_when_access_token_is_missing()
    {
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: MakeAccountProfileWithCharacter("Aelrin", "silvermoon"));

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal(accessToken: null));
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, objectResult.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Blizzard fetch failure → 502
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_502_when_blizzard_profile_fetch_fails()
    {
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: MakeAccountProfileWithCharacter("Aelrin", "silvermoon"));

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient
            .Setup(p => p.GetCharacterProfileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Blizzard API down"));

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, objectResult.StatusCode);

        repo.Verify(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Validator: invalid region → 400
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_400_when_region_is_invalid()
    {
        var repo = new Mock<IRaidersRepository>();
        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "xx", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        Assert.IsType<ProblemDetails>(badRequest.Value);

        repo.Verify(r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Raider not found → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_404_when_raider_not_found()
    {
        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());
        var req = MakePostRequest(new { region = "eu", realm = "silvermoon", name = "Aelrin" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#raider-not-found", problem.Type);
    }

    // -------------------------------------------------------------------------
    // Tiered fetch — only stale tiers are fetched
    // -------------------------------------------------------------------------

    private static object ValidBody => new { region = "eu", realm = "silvermoon", name = "Aelrin" };

    private static SessionPrincipal MakePrincipal(string? accessToken = FakeAccessToken) =>
        FakePrincipal(accessToken);

    private static StoredBlizzardAccountProfile OwningSummary() =>
        MakeAccountProfileWithCharacter("Aelrin", "silvermoon");

    private static RaiderDocument MakeRaider(
        IReadOnlyList<StoredSelectedCharacter>? characters = null,
        StoredBlizzardAccountProfile? accountProfile = null) =>
        new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: accountProfile ?? OwningSummary(),
            Characters: characters);

    private static Mock<IRaidersRepository> RepoReturning(RaiderDocument raider)
    {
        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    [Fact]
    public async Task Run_skips_profile_and_media_fetches_when_tiers_are_fresh()
    {
        var now = DateTimeOffset.UtcNow;
        var existing = new StoredSelectedCharacter(
            Id: "eu-silvermoon-aelrin", Region: "eu", Realm: "silvermoon", Name: "Aelrin",
            Level: 80,
            SpecializationsSummary: new StoredSpecializationsSummary(), // non-null sentinel
            ProfileFetchedAt: now.AddMinutes(-30).ToString("O"),
            SpecsFetchedAt: now.AddMinutes(-30).ToString("O"),
            MediaFetchedAt: now.AddHours(-1).ToString("O"));

        var raider = MakeRaider(characters: [existing], accountProfile: OwningSummary());
        var repo = RepoReturning(raider);
        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient.Setup(p => p.GetCharacterSpecializationsAsync("silvermoon", "aelrin", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new CharacterSpecializationsResponse());

        var fn = new RaiderCharacterAddFunction(repo.Object, profileClient.Object, NullLogger<RaiderCharacterAddFunction>.Instance);
        await fn.Run(MakePostRequest(ValidBody), MakeFunctionContext(MakePrincipal()), CancellationToken.None);

        profileClient.Verify(p => p.GetCharacterProfileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        profileClient.Verify(p => p.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        profileClient.Verify(p => p.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
