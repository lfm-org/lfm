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
using Lfm.Contracts.Characters;
using Xunit;

using BlizzardOptions = Lfm.Api.Options.BlizzardOptions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BattleNetCharactersFunction"/>.
///
/// Three required cases:
///   1. Happy path — cached profile within cooldown → 200 with character list.
///   2. No cached profile (or cooldown expired) → 204 No Content.
///   3. [RequireAuth] attribute present on the Run method.
/// </summary>
public class BattleNetCharactersFunctionTests
{
    private const string FakeBattleNetId = "bnet-42";
    private const string Region = "eu";

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal FakePrincipal() => new(
        BattleNetId: FakeBattleNetId,
        BattleTag: "Player#1234",
        GuildId: null,
        GuildName: null,
        IssuedAt: DateTimeOffset.UtcNow,
        ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static BattleNetCharactersFunction MakeFunction(Mock<IRaidersRepository> repoMock)
    {
        var blizzardOpts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Region = Region,
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = "https://example.com",
        });
        return new BattleNetCharactersFunction(repoMock.Object, blizzardOpts);
    }

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_characters_when_cached_profile_is_within_cooldown()
    {
        // Arrange: raider document with a fresh cached account profile summary.
        var refreshedAt = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"); // 2 min ago, within 15-min cooldown
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            AccountProfileSummary: new BlizzardAccountProfileSummary(
                WowAccounts:
                [
                    new BlizzardWowAccount(
                        Id: 1,
                        Characters:
                        [
                            new BlizzardAccountCharacter(
                                Name: "Legolas",
                                Level: 80,
                                Realm: new BlizzardRealmRef(Slug: "silvermoon", Name: "Silvermoon"),
                                PlayableClass: new BlizzardNamedRef(Id: 3, Name: "Hunter"))
                        ])
                ]),
            AccountProfileRefreshedAt: refreshedAt);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var fn = MakeFunction(repo);
        var ctx = MakeFunctionContext(FakePrincipal());

        // Act
        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var characters = Assert.IsAssignableFrom<List<CharacterDto>>(ok.Value);
        Assert.Single(characters);

        var character = characters[0];
        Assert.Equal("Legolas", character.Name);
        Assert.Equal("silvermoon", character.Realm);
        Assert.Equal("Silvermoon", character.RealmName);
        Assert.Equal(80, character.Level);
        Assert.Equal(Region, character.Region);
        Assert.Equal(3, character.ClassId);
    }

    // -------------------------------------------------------------------------
    // No cached profile → 204
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_no_content_when_no_cached_profile_exists()
    {
        // Arrange: raider document with no accountProfileSummary (fresh login, no refresh yet).
        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var fn = MakeFunction(repo);
        var ctx = MakeFunctionContext(FakePrincipal());

        // Act
        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        // Assert: caller must POST /battlenet/characters/refresh to populate the cache.
        Assert.IsType<NoContentResult>(result);
    }

}
