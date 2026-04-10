using System.Net;
using FluentAssertions;
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

        result.Should().NotBeNull();
        result!.BattleNetId.Should().Be("player#1234");
        result.GuildName.Should().Be("Stormchasers");
        result.Locale.Should().Be("en");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/me");
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_TaskCanceledException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new TaskCanceledException()));

        var result = await client.GetAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_returns_null_on_5xx_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_patches_and_returns_response_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new UpdateMeResponse("fi")));

        var result = await client.UpdateAsync(new UpdateMeRequest("fi"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Locale.Should().Be("fi");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/me");
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));

        var result = await client.UpdateAsync(new UpdateMeRequest("xx"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("connection refused")));

        var result = await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        result.Should().BeNull();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_true_on_success()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NoContent));

        var result = await client.DeleteAsync(CancellationToken.None);

        result.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/me");
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var result = await client.DeleteAsync(CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_on_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network unreachable")));

        var result = await client.DeleteAsync(CancellationToken.None);

        result.Should().BeFalse();
    }
}
