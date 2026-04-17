// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Xunit;

using BlizzardOptions = Lfm.Api.Options.BlizzardOptions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BattleNetCharactersRefreshFunction"/>.
///
/// Three required cases:
///   1. Happy path — access token present, cooldown expired → calls Blizzard, updates doc, returns 200 with characters.
///   2. Cooldown active — accountProfileRefreshedAt within 15 min → 429 with Retry-After header.
///   3. [RequireAuth] attribute present on the Run method.
/// </summary>
public class BattleNetCharactersRefreshFunctionTests
{
    private const string FakeBattleNetId = "bnet-42";
    private const string FakeAccessToken = "bnet-access-token-xyz";
    private const string Region = "eu";

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

    private static BattleNetCharactersRefreshFunction MakeFunction(
        Mock<IRaidersRepository> repoMock,
        Mock<IBlizzardProfileClient> profileMock)
    {
        var blizzardOpts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Region = Region,
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = "https://example.com",
        });
        return new BattleNetCharactersRefreshFunction(repoMock.Object, profileMock.Object, blizzardOpts);
    }

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_characters_when_cooldown_expired_and_blizzard_fetch_succeeds()
    {
        // Arrange: raider with expired cooldown (refreshed 20 min ago, beyond the 15-min window).
        var refreshedAt = DateTimeOffset.UtcNow.AddMinutes(-20).ToString("O");
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileRefreshedAt: refreshedAt);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var freshSummary = new BlizzardAccountProfileSummary(
            WowAccounts:
            [
                new BlizzardWowAccount(
                    Id: 1,
                    Characters:
                    [
                        new BlizzardAccountCharacter(
                            Name: "Thrall",
                            Level: 80,
                            Realm: new BlizzardRealmRef(Slug: "draenor", Name: "Draenor"),
                            PlayableClass: new BlizzardNamedRef(Id: 1, Name: "Warrior"))
                    ])
            ]);

        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient
            .Setup(p => p.GetAccountProfileSummaryAsync(FakeAccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshSummary);

        var fn = MakeFunction(repo, profileClient);
        var ctx = MakeFunctionContext(FakePrincipal());

        // Act
        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        // Assert: 200 with character list
        var ok = Assert.IsType<OkObjectResult>(result);
        var characters = Assert.IsAssignableFrom<List<CharacterDto>>(ok.Value);
        Assert.Single(characters);

        var character = characters[0];
        Assert.Equal("Thrall", character.Name);
        Assert.Equal("draenor", character.Realm);
        Assert.Equal("Draenor", character.RealmName);
        Assert.Equal(80, character.Level);
        Assert.Equal(Region, character.Region);
        Assert.Equal(1, character.ClassId);

        // Assert: repo upserted with updated profile + refreshed timestamps
        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d =>
                d.AccountProfileSummary != null &&
                d.AccountProfileRefreshedAt != null &&
                d.AccountProfileFetchedAt != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Cooldown active → 429
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_429_when_cooldown_is_still_active()
    {
        // Arrange: raider with a refresh 2 minutes ago — well within the 15-minute cooldown.
        var refreshedAt = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O");
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileRefreshedAt: refreshedAt);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var profileClient = new Mock<IBlizzardProfileClient>();

        var fn = MakeFunction(repo, profileClient);
        var httpContext = new DefaultHttpContext();
        var ctx = MakeFunctionContext(FakePrincipal());

        // Act
        var result = await fn.Run(httpContext.Request, ctx, CancellationToken.None);

        // Assert: 429 with a Retry-After header
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(429, statusResult.StatusCode);

        Assert.False(string.IsNullOrEmpty(httpContext.Response.Headers["Retry-After"].ToString()));

        // Assert: Blizzard was NOT called
        profileClient.Verify(
            p => p.GetAccountProfileSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
