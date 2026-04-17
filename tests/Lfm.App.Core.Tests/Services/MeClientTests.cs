// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Services;
using Lfm.Contracts.Me;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class MeClientTests
{
    private static (MeClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new MeClient(factory.Object), handler);
    }

    private static MeResponse MakeMeResponse(string locale = "en") =>
        new(
            BattleNetId: "player#1234",
            GuildName: "Stormchasers",
            SelectedCharacterId: "eu-silvermoon-sourgeezer",
            IsSiteAdmin: false,
            Locale: locale);

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_returns_me_response_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeMeResponse()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("player#1234", result!.BattleNetId);
        Assert.Equal("Stormchasers", result.GuildName);
        Assert.Equal("en", result.Locale);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/me", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_TaskCanceledException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new TaskCanceledException()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_on_5xx_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_patches_and_returns_response_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new UpdateMeResponse("fi")));

        var result = await client.UpdateAsync(new UpdateMeRequest("fi"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("fi", result!.Locale);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("/api/me", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));

        var result = await client.UpdateAsync(new UpdateMeRequest("xx"), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("connection refused")));

        var result = await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_true_on_success()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NoContent));

        var result = await client.DeleteAsync(CancellationToken.None);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("/api/me", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_non_success_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var result = await client.DeleteAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network unreachable")));

        var result = await client.DeleteAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1, handler.CallCount);
    }

    // ── SelectCharacterAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectCharacterAsync_returns_true_on_200()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.OK));

        var result = await client.SelectCharacterAsync("eu-silvermoon-arthas", CancellationToken.None);

        Assert.True(result);
        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal("/api/raider/characters/eu-silvermoon-arthas", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task SelectCharacterAsync_returns_false_on_403()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var result = await client.SelectCharacterAsync("eu-silvermoon-arthas", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SelectCharacterAsync_returns_false_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network error")));

        var result = await client.SelectCharacterAsync("eu-silvermoon-arthas", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1, handler.CallCount);
    }
}
