using System.Net;
using System.Text.Json;
using FluentAssertions;
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
    public async Task GetCharactersAsync_returns_characters_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new[] { MakeCharacter("Char-A"), MakeCharacter("Char-B") }));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result[0].Name.Should().Be("Char-A");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/battlenet/characters");
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

        result.Should().NotBeNull();
        result!.Should().ContainSingle();
        result[0].Name.Should().Be("Lower");
        result[0].Region.Should().Be("eu");
    }

    [Fact]
    public async Task GetCharactersAsync_returns_null_when_server_returns_5xx()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCharactersAsync_returns_null_when_handler_throws_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCharactersAsync_returns_null_when_handler_throws_JsonException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new JsonException("bad payload")));

        var result = await client.GetCharactersAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCharactersAsync_lets_OutOfMemoryException_propagate()
    {
        // Regression guard for the BattleNetClient typed-filter fix (commit 8d6cad8) —
        // the client must NOT swallow critical exceptions like OutOfMemoryException.
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new OutOfMemoryException("oom")));

        var act = () => client.GetCharactersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<OutOfMemoryException>();
    }

    // ── RefreshCharactersAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RefreshCharactersAsync_posts_and_returns_characters_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(
            HttpStatusCode.OK,
            new[] { MakeCharacter("Refreshed") }));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().ContainSingle().Which.Name.Should().Be("Refreshed");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/battlenet/characters/refresh");
    }

    [Fact]
    public async Task RefreshCharactersAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadGateway));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCharactersAsync_returns_null_on_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("boom")));

        var result = await client.RefreshCharactersAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCharactersAsync_lets_StackOverflowException_propagate()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new StackOverflowException()));

        var act = () => client.RefreshCharactersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<StackOverflowException>();
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

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result["eu-silvermoon-sourgeezer"].Should().Be("https://render.example/p1.jpg");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/battlenet/character-portraits");
        handler.LastRequest.Content.Should().NotBeNull();
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPortraitsAsync_returns_null_on_TaskCanceledException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new TaskCanceledException()));

        var result = await client.GetPortraitsAsync(
            new[] { new CharacterPortraitRequest("eu", "silvermoon", "x") },
            CancellationToken.None);

        result.Should().BeNull();
    }
}
