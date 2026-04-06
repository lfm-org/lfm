using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Raiders;
using Xunit;

namespace Lfm.Api.Tests;

public class RaiderCharacterFunctionTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static HttpRequest MakePutRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "PUT";
        return httpContext.Request;
    }

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-1",
        IReadOnlyList<StoredSelectedCharacter>? characters = null,
        string? selectedCharacterId = null) =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: selectedCharacterId,
            Locale: null,
            Characters: characters ?? [
                new StoredSelectedCharacter(
                    Id: "eu-silvermoon-aelrin",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Aelrin")
            ]);

    // ------------------------------------------------------------------
    // Test 1: Happy path — character is in the list, selectedCharacterId updated
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_200_and_updates_selected_character_when_character_is_owned()
    {
        var principal = MakePrincipal();
        var raider = MakeRaiderDoc();

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var fn = new RaiderCharacterFunction(repo.Object);
        var result = await fn.Run(MakePutRequest(), "eu-silvermoon-aelrin", MakeFunctionContext(principal), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<UpdateCharacterResponse>().Subject;
        response.SelectedCharacterId.Should().Be("eu-silvermoon-aelrin");

        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d => d.SelectedCharacterId == "eu-silvermoon-aelrin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Character not in stored list — 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_when_character_is_not_in_raider_characters_list()
    {
        var principal = MakePrincipal();
        var raider = MakeRaiderDoc(); // has "eu-silvermoon-aelrin", not the requested ID

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var fn = new RaiderCharacterFunction(repo.Object);
        var result = await fn.Run(MakePutRequest(), "eu-silvermoon-unknownchar", MakeFunctionContext(principal), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);

        repo.Verify(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: [RequireAuth] attribute is present on Run method
    // ------------------------------------------------------------------

    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(RaiderCharacterFunction).GetMethod(nameof(RaiderCharacterFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "RaiderCharacterFunction.Run must carry [RequireAuth] for AuthPolicyMiddleware to enforce 401");
    }
}
