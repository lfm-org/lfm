// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="BattleNetCharacterPortraitsFunction"/>.
///
/// Three required cases:
///   1. Happy path (cached) — portrait URL present in portraitCache → returned directly, no Blizzard call.
///   2. Cache miss — portrait not cached → service fetches from Blizzard, result returned.
///   3. [RequireAuth] attribute present on the Run method.
/// </summary>
public class BattleNetCharacterPortraitsFunctionTests
{
    private const string FakeBattleNetId = "bnet-42";
    private const string FakeAccessToken = "bnet-access-token-xyz";

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

    private static BattleNetCharacterPortraitsFunction MakeFunction(
        Mock<IRaidersRepository> repoMock,
        Mock<ICharacterPortraitService> portraitServiceMock)
        => new(repoMock.Object, portraitServiceMock.Object);

    // -------------------------------------------------------------------------
    // Happy path — portrait cached in portraitCache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_cached_portrait_url_without_calling_blizzard()
    {
        // Arrange: raider document with a portrait already in the cache (Blizzard CDN URL).
        const string CharacterId = "eu-silvermoon-legolas";
        const string CachedPortraitUrl = "https://render.worldofwarcraft.com/eu/character/silvermoon/1/avatar.jpg";

        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null,
            PortraitCache: new Dictionary<string, string>
            {
                [CharacterId] = CachedPortraitUrl,
            });

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        // Portrait service resolves from the raider doc — returns the cached URL.
        var portraitService = new Mock<ICharacterPortraitService>();
        portraitService
            .Setup(s => s.ResolveAsync(
                raider,
                It.IsAny<IReadOnlyList<CharacterPortraitRequest>>(),
                FakeAccessToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortraitResponse(new Dictionary<string, string>
            {
                [CharacterId] = CachedPortraitUrl,
            }));

        var fn = MakeFunction(repo, portraitService);
        var ctx = MakeFunctionContext(FakePrincipal());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        var bodyJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { region = "eu", realm = "silvermoon", name = "Legolas" },
        });
        httpContext.Request.Body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(bodyJson));

        // Act
        var result = await fn.Run(httpContext.Request, ctx, CancellationToken.None);

        // Assert: 200 with the cached portrait URL
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<PortraitResponse>(ok.Value);
        Assert.True(response.Portraits.ContainsKey(CharacterId));
        Assert.Equal(CachedPortraitUrl, response.Portraits[CharacterId]);
    }

    // -------------------------------------------------------------------------
    // Cache miss — portrait fetched from Blizzard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Fetches_portrait_from_blizzard_when_not_in_cache()
    {
        // Arrange: raider with no portrait cache — service must call Blizzard.
        const string CharacterId = "eu-draenor-thrall";
        const string BlizzardPortraitUrl = "https://render.worldofwarcraft.com/eu/character/draenor/5/thrall-avatar.jpg";

        var raider = new RaiderDocument(
            Id: FakeBattleNetId,
            BattleNetId: FakeBattleNetId,
            SelectedCharacterId: null,
            Locale: null);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        // Portrait service fetches from Blizzard and returns the URL.
        var portraitService = new Mock<ICharacterPortraitService>();
        portraitService
            .Setup(s => s.ResolveAsync(
                raider,
                It.IsAny<IReadOnlyList<CharacterPortraitRequest>>(),
                FakeAccessToken,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortraitResponse(new Dictionary<string, string>
            {
                [CharacterId] = BlizzardPortraitUrl,
            }));

        var fn = MakeFunction(repo, portraitService);
        var ctx = MakeFunctionContext(FakePrincipal());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        var bodyJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { region = "eu", realm = "draenor", name = "Thrall" },
        });
        httpContext.Request.Body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(bodyJson));

        // Act
        var result = await fn.Run(httpContext.Request, ctx, CancellationToken.None);

        // Assert: 200 with Blizzard-fetched portrait URL
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsAssignableFrom<PortraitResponse>(ok.Value);
        Assert.True(response.Portraits.ContainsKey(CharacterId));
        Assert.Equal(BlizzardPortraitUrl, response.Portraits[CharacterId]);

        // Verify service was called with the access token
        portraitService.Verify(
            s => s.ResolveAsync(
                raider,
                It.IsAny<IReadOnlyList<CharacterPortraitRequest>>(),
                FakeAccessToken,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // -------------------------------------------------------------------------
    // Raider not found → 404
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Returns_404_when_raider_not_found()
    {
        // Arrange: raider document does not exist in Cosmos.
        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(FakeBattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var portraitService = new Mock<ICharacterPortraitService>();

        var fn = MakeFunction(repo, portraitService);
        var ctx = MakeFunctionContext(FakePrincipal());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        var bodyJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { region = "eu", realm = "silvermoon", name = "Legolas" },
        });
        httpContext.Request.Body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(bodyJson));

        // Act
        var result = await fn.Run(httpContext.Request, ctx, CancellationToken.None);

        // Assert: 404
        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#raider-not-found", problem.Type);

        // Service should not be called when raider doesn't exist.
        portraitService.Verify(
            s => s.ResolveAsync(
                It.IsAny<RaiderDocument>(),
                It.IsAny<IReadOnlyList<CharacterPortraitRequest>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
}
