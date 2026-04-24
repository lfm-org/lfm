// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Text.Json;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class BattleNetClientTests
{
    private static (BattleNetClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new BattleNetClient(factory.Object), handler);
    }

    private static CharacterDto MakeCharacter(string name = "Sourgeezer") =>
        new(
            Name: name,
            Realm: "silvermoon",
            RealmName: "Silvermoon",
            Level: 80,
            Region: "eu",
            ClassId: 5,
            ClassName: "Priest",
            PortraitUrl: "https://render.example/portrait.jpg",
            ActiveSpecId: 257,
            SpecName: "Holy");

    // ── GetCharactersAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCharactersAsync_returns_Cached_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new[] { MakeCharacter("Char-A"), MakeCharacter("Char-B") }));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        var cached = Assert.IsType<CharactersFetchResult.Cached>(result);
        Assert.Equal(2, cached.Characters.Count);
        Assert.Equal("Char-A", cached.Characters[0].Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/battlenet/characters", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetCharactersAsync_uses_case_insensitive_json_deserialization()
    {
        // Server returns lowerCamel JSON; client must deserialize despite C# PascalCase records.
        var json = "[{\"name\":\"Lower\",\"realm\":\"silvermoon\",\"realmName\":\"Silvermoon\",\"level\":80,\"region\":\"eu\"}]";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
        var (client, _) = MakeClient(handler);

        var result = await client.GetCharactersAsync(CancellationToken.None);

        var cached = Assert.IsType<CharactersFetchResult.Cached>(result);
        var single = Assert.Single(cached.Characters);
        Assert.Equal("Lower", single.Name);
        Assert.Equal("eu", single.Region);
    }

    [Fact]
    public async Task GetCharactersAsync_returns_NeedsRefresh_on_204()
    {
        // Backend returns 204 when no cached account profile exists (or the
        // 15-minute cooldown expired), signalling the caller must POST to
        // /api/battlenet/characters/refresh. Regression guard for the bug where
        // /characters showed "load failed" on first visit until the user
        // clicked refresh manually.
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NoContent));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        Assert.IsType<CharactersFetchResult.NeedsRefresh>(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetCharactersAsync_returns_Error_when_server_returns_5xx()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        Assert.IsType<CharactersFetchResult.Error>(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetCharactersAsync_returns_Error_when_handler_throws_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        Assert.IsType<CharactersFetchResult.Error>(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetCharactersAsync_returns_Error_when_handler_throws_JsonException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new JsonException("bad payload")));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        Assert.IsType<CharactersFetchResult.Error>(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetCharactersAsync_lets_OutOfMemoryException_propagate()
    {
        // Regression guard for the BattleNetClient typed-filter fix (commit 8d6cad8) —
        // the client must NOT swallow critical exceptions like OutOfMemoryException.
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new OutOfMemoryException("oom")));

        await Assert.ThrowsAsync<OutOfMemoryException>(() => client.GetCharactersAsync(CancellationToken.None));
    }

    // ── RefreshCharactersAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RefreshCharactersAsync_posts_and_returns_characters_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new[] { MakeCharacter("Refreshed") }));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        Assert.NotNull(result);
        var single = Assert.Single(result!);
        Assert.Equal("Refreshed", single.Name);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/battlenet/characters/refresh", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task RefreshCharactersAsync_returns_null_on_non_success_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadGateway));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RefreshCharactersAsync_returns_null_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RefreshCharactersAsync_lets_StackOverflowException_propagate()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new StackOverflowException()));

        await Assert.ThrowsAsync<StackOverflowException>(() => client.RefreshCharactersAsync(CancellationToken.None));
    }

    // ── GetPortraitsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetPortraitsAsync_returns_portraits_dictionary_on_success()
    {
        var response = new PortraitResponse(new Dictionary<string, string>
        {
            ["eu-silvermoon-sourgeezer"] = "https://render.example/p1.jpg",
            ["eu-silvermoon-other"] = "https://render.example/p2.jpg",
        });
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, response));
        var requests = new[]
        {
            new CharacterPortraitRequest("eu", "silvermoon", "sourgeezer"),
            new CharacterPortraitRequest("eu", "silvermoon", "other"),
        };

        var result = await client.GetPortraitsAsync(requests, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("https://render.example/p1.jpg", result["eu-silvermoon-sourgeezer"]);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/battlenet/character-portraits", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.NotNull(handler.LastRequest.Content);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_on_non_success_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_on_TaskCanceledException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new TaskCanceledException()));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_when_handler_throws_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_when_handler_throws_JsonException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new JsonException("bad payload")));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetPortraitsAsync_lets_OutOfMemoryException_propagate()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new OutOfMemoryException("oom")));

        await Assert.ThrowsAsync<OutOfMemoryException>(() => client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None));
    }
}
