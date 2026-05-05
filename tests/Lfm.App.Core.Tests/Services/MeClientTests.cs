// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Json;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
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
            SelectedCharacter: null,
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
        Assert.Equal("/api/v1/me", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_handler_throws_TaskCanceledException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new TaskCanceledException()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_on_5xx_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_retries_transient_503_and_returns_me_response()
    {
        var responses = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse())
        ]);
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ => responses.Dequeue()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("player#1234", result!.BattleNetId);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetAsync_retries_each_transient_status_and_returns_me_response(HttpStatusCode transientStatus)
    {
        var responses = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(transientStatus),
            StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse())
        ]);
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ => responses.Dequeue()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_does_not_retry_non_transient_status()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.BadRequest));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_retry_delay_is_canceled()
    {
        using var cts = new CancellationTokenSource();
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ =>
        {
            cts.Cancel();
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }));

        var result = await client.GetAsync(cts.Token);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_does_not_retry_401()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Unauthorized));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_unauthorized_clears_cached_etag_before_later_update()
    {
        var requests = new List<HttpRequestMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse());
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v1\"");
        var responses = new Queue<HttpResponseMessage>([
            getResponse,
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("en"))
        ]);
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));

        await client.GetAsync(CancellationToken.None);
        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.False(requests[2].Headers.Contains("If-Match"));
    }

    [Fact]
    public async Task GetAsync_null_json_body_clears_cached_etag()
    {
        var requests = new List<HttpRequestMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse());
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v1\"");
        var nullGetResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<MeResponse?>(null),
        };
        nullGetResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v2\"");
        var responses = new Queue<HttpResponseMessage>([
            getResponse,
            nullGetResponse,
            StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("en"))
        ]);
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));

        await client.GetAsync(CancellationToken.None);
        var nullResult = await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.Null(nullResult);
        Assert.False(requests[2].Headers.Contains("If-Match"));
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
        Assert.Equal("/api/v1/me", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UpdateAsync_sends_etag_captured_from_get()
    {
        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse());
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v1\"");
        responses.Enqueue(getResponse);
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("en")));
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));

        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.True(requests[1].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"me-etag-v1\"", ifMatch!.Single());
    }

    [Fact]
    public async Task UpdateAsync_replaces_cached_etag_from_patch_response()
    {
        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse());
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v1\"");
        responses.Enqueue(getResponse);
        var firstPatchResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("fi"));
        firstPatchResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v2\"");
        responses.Enqueue(firstPatchResponse);
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("en")));
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));

        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("fi"), CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.True(requests[2].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"me-etag-v2\"", ifMatch!.Single());
    }

    [Fact]
    public async Task UpdateAsync_null_json_body_clears_cached_etag()
    {
        var requests = new List<HttpRequestMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeMeResponse());
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v1\"");
        var nullPatchResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<UpdateMeResponse?>(null),
        };
        nullPatchResponse.Headers.TryAddWithoutValidation("ETag", "\"me-etag-v2\"");
        var responses = new Queue<HttpResponseMessage>([
            getResponse,
            nullPatchResponse,
            StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, new UpdateMeResponse("en"))
        ]);
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));

        await client.GetAsync(CancellationToken.None);
        var nullResult = await client.UpdateAsync(new UpdateMeRequest("fi"), CancellationToken.None);
        await client.UpdateAsync(new UpdateMeRequest("en"), CancellationToken.None);

        Assert.Null(nullResult);
        Assert.True(requests[1].Headers.Contains("If-Match"));
        Assert.False(requests[2].Headers.Contains("If-Match"));
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
        Assert.Equal("/api/v1/me", handler.LastRequest.RequestUri!.PathAndQuery);
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
        Assert.Equal("/api/v1/raider/characters/eu-silvermoon-arthas", handler.LastRequest.RequestUri!.PathAndQuery);
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

    // ── EnrichCharacterAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task EnrichCharacterAsync_returns_dto_on_200()
    {
        var dto = new CharacterDto(
            Name: "Sourgeezer",
            Realm: "silvermoon",
            RealmName: "Silvermoon",
            Level: 80,
            Region: "eu",
            ClassId: 5,
            ClassName: "Priest");
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, dto));

        var result = await client.EnrichCharacterAsync("eu-silvermoon-sourgeezer", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Sourgeezer", result!.Name);
        Assert.Equal("silvermoon", result.Realm);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/raider/characters/eu-silvermoon-sourgeezer/enrich", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task EnrichCharacterAsync_returns_null_on_non_success()
    {
        var (client, handler) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var result = await client.EnrichCharacterAsync("eu-silvermoon-sourgeezer", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task EnrichCharacterAsync_returns_null_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network error")));

        var result = await client.EnrichCharacterAsync("eu-silvermoon-sourgeezer", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }
}
