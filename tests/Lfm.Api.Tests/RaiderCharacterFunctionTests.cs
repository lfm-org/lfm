// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
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

    private const string FakeInvocationId = "00000000-0000-0000-0000-000000000001";

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        ctx.Setup(c => c.InvocationId).Returns(FakeInvocationId);
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

        var fn = new RaiderCharacterFunction(repo.Object, new TestLogger<RaiderCharacterFunction>());
        var result = await fn.Run(MakePutRequest(), "eu-silvermoon-aelrin", MakeFunctionContext(principal), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UpdateCharacterResponse>(ok.Value);
        Assert.Equal("eu-silvermoon-aelrin", response.SelectedCharacterId);

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

        var fn = new RaiderCharacterFunction(repo.Object, new TestLogger<RaiderCharacterFunction>());
        var result = await fn.Run(MakePutRequest(), "eu-silvermoon-unknownchar", MakeFunctionContext(principal), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        repo.Verify(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: Upsert failure — 500 with no exception detail in the body
    // ------------------------------------------------------------------
    //
    // Pin the contract that the 500 path never echoes `ex.Message` or
    // `ex.GetType().Name` to the caller. CosmosException messages typically
    // embed AccountEndpoint / diagnostic JSON; a refactor that stops routing
    // through `InternalErrorResult.Create` would re-leak them.

    [Fact]
    public async Task Run_returns_generic_500_without_exception_detail_when_upsert_throws()
    {
        var principal = MakePrincipal();
        var raider = MakeRaiderDoc();

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException(
                "ProvisioningRU exhausted. AccountEndpoint=https://secret.documents.azure.com; AccountKey=topsecret",
                System.Net.HttpStatusCode.TooManyRequests, 0, "", 0));

        var fn = new RaiderCharacterFunction(repo.Object, new TestLogger<RaiderCharacterFunction>());
        var result = await fn.Run(MakePutRequest(), "eu-silvermoon-aelrin", MakeFunctionContext(principal), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);

        var json = JsonSerializer.Serialize(obj.Value);
        Assert.DoesNotContain("ProvisioningRU", json);
        Assert.DoesNotContain("AccountEndpoint", json);
        Assert.DoesNotContain("AccountKey", json);
        Assert.DoesNotContain("CosmosException", json);
        Assert.Contains("internal-error", json);
        Assert.Contains(FakeInvocationId, json);
    }
}
