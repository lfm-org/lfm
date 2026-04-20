// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class RaiderCharacterEnrichFunctionTests
{
    private static readonly string CharId = "eu-stormreaver-shalena";

    [Fact]
    public async Task Returns_404_when_raider_missing()
    {
        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var fn = MakeFunction(repo.Object, new Mock<IBlizzardProfileClient>().Object);
        var result = await fn.Run(MakeRequest(), CharId, MakeCtx(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Returns_403_when_character_not_owned()
    {
        var raider = MakeRaider(accountChars: [("different-realm", "other")]);
        var repo = RepoReturning(raider);
        var fn = MakeFunction(repo.Object, new Mock<IBlizzardProfileClient>().Object);
        var result = await fn.Run(MakeRequest(), CharId, MakeCtx(), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }

    [Fact]
    public async Task Does_not_mutate_SelectedCharacterId()
    {
        var raider = MakeRaider(
            selected: "eu-silvermoon-aelrin",
            accountChars: [("stormreaver", "Shalena")]);
        var repo = RepoReturning(raider);
        var profileClient = MakeProfileClient();

        var fn = MakeFunction(repo.Object, profileClient.Object);
        await fn.Run(MakeRequest(), CharId, MakeCtx(), CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d => d.SelectedCharacterId == "eu-silvermoon-aelrin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Writes_new_character_into_raider_characters()
    {
        var raider = MakeRaider(accountChars: [("stormreaver", "Shalena")]);
        var repo = RepoReturning(raider);
        var fn = MakeFunction(repo.Object, MakeProfileClient().Object);
        var result = await fn.Run(MakeRequest(), CharId, MakeCtx(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d => d.Characters != null
                && d.Characters.Any(c => c.Id == CharId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Calls_Blizzard_with_dashed_realm_slug_intact()
    {
        const string dashedId = "eu-the-maelstrom-kalmatar";
        var raider = MakeRaider(accountChars: [("the-maelstrom", "Kalmatar")]);
        var repo = RepoReturning(raider);
        var profileClient = MakeProfileClient();

        var fn = MakeFunction(repo.Object, profileClient.Object);
        await fn.Run(MakeRequest(), dashedId, MakeCtx(), CancellationToken.None);

        profileClient.Verify(p => p.GetCharacterProfileAsync(
            "the-maelstrom", "kalmatar", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Returns_404_when_Blizzard_returns_404()
    {
        var raider = MakeRaider(accountChars: [("stormreaver", "Shalena")]);
        var repo = RepoReturning(raider);
        var profileClient = new Mock<IBlizzardProfileClient>();
        profileClient.Setup(p => p.GetCharacterProfileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException(
                "Not Found", inner: null, statusCode: System.Net.HttpStatusCode.NotFound));

        var fn = MakeFunction(repo.Object, profileClient.Object);
        var result = await fn.Run(MakeRequest(), CharId, MakeCtx(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var body = JsonSerializer.SerializeToElement(notFound.Value);
        Assert.Equal("Character not found on Blizzard", body.GetProperty("error").GetString());
    }

    // ---- helpers ----
    private static RaiderCharacterEnrichFunction MakeFunction(
        IRaidersRepository repo, IBlizzardProfileClient profile)
        => new(repo, profile, NullLogger<RaiderCharacterEnrichFunction>.Instance);

    private static HttpRequest MakeRequest() => new DefaultHttpContext().Request;

    private static FunctionContext MakeCtx()
    {
        var items = new Dictionary<object, object>
        {
            [SessionKeys.Principal] = new SessionPrincipal(
                BattleNetId: "bnet-1", BattleTag: "P#1",
                GuildId: null, GuildName: null,
                IssuedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
                AccessToken: "tok")
        };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static Mock<IRaidersRepository> RepoReturning(RaiderDocument raider)
    {
        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo;
    }

    private static RaiderDocument MakeRaider(
        string? selected = null,
        IReadOnlyList<StoredSelectedCharacter>? characters = null,
        IEnumerable<(string Realm, string Name)>? accountChars = null)
        => new(
            Id: "bnet-1", BattleNetId: "bnet-1",
            SelectedCharacterId: selected, Locale: null,
            Characters: characters,
            AccountProfileSummary: accountChars is null ? null : new BlizzardAccountProfileSummary(
                WowAccounts: [new BlizzardWowAccount(
                    Id: 1,
                    Characters: accountChars.Select(c =>
                        new BlizzardAccountCharacter(
                            Name: c.Name, Level: 80,
                            Realm: new BlizzardRealmRef(Slug: c.Realm))).ToList())]));

    private static Mock<IBlizzardProfileClient> MakeProfileClient()
    {
        var m = new Mock<IBlizzardProfileClient>();
        m.Setup(p => p.GetCharacterProfileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new BlizzardCharacterProfileResponse(Name: "Shalena", Level: 80));
        m.Setup(p => p.GetCharacterSpecializationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(new BlizzardCharacterSpecializationsResponse());
        m.Setup(p => p.GetCharacterMediaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((BlizzardCharacterMediaSummary?)null);
        return m;
    }
}
