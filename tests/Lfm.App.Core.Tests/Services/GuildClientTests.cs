// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Services;
using Lfm.Contracts.Guild;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class GuildClientTests
{
    private static (GuildClient client, StubHttpMessageHandler handler) MakeClient(
        StubHttpMessageHandler handler,
        IDataCache? cache = null,
        TimeProvider? timeProvider = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return ((cache, timeProvider) switch
        {
            (null, null) => new GuildClient(factory.Object),
            (null, { } clock) => new GuildClient(factory.Object, clock),
            ({ } dataCache, null) => new GuildClient(factory.Object, dataCache),
            ({ } dataCache, { } clock) => new GuildClient(factory.Object, dataCache, clock),
        }, handler);
    }

    private static GuildDto MakeGuildDto(string? name = "Stormchasers") =>
        new(
            Guild: name is null ? null : new GuildInfoDto(
                Id: 1,
                Name: name,
                Slogan: "We ride the storm",
                RealmName: "Silvermoon",
                FactionName: "Alliance",
                MemberCount: 120,
                RankCount: 10,
                CrestEmblemUrl: null,
                CrestBorderUrl: null),
            Setup: new GuildSetupDto(
                IsInitialized: true,
                RequiresSetup: false,
                RankDataFresh: true,
                Timezone: "Europe/Helsinki",
                Locale: "fi"),
            Settings: null,
            Editor: new GuildEditorDto(CanEdit: false),
            MemberPermissions: new GuildMemberPermissionsDto(
                CanCreateGuildRuns: true,
                CanSignupGuildRuns: true,
                CanDeleteGuildRuns: false));

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_returns_guild_dto_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto()));

        var result = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Stormchasers", result!.Guild!.Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/guild", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetAsync_returns_cached_guild_on_repeated_successful_call()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn")));
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ => responses.Dequeue()));

        var first = await client.GetAsync(CancellationToken.None);
        var second = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Stormchasers", second!.Guild!.Name);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_refetches_after_guild_cache_invalidation()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn")));
        var cache = new InMemoryDataCache();
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ => responses.Dequeue()), cache);

        var first = await client.GetAsync(CancellationToken.None);
        cache.Invalidate(DataCacheKeys.Guild);
        var second = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Stormchasers Reborn", second!.Guild!.Name);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_refetches_only_after_current_guild_cache_ttl_expires()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn")));
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero));
        var (client, handler) = MakeClient(new StubHttpMessageHandler(_ => responses.Dequeue()), timeProvider: timeProvider);

        var first = await client.GetAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(2).Add(TimeSpan.FromMilliseconds(1)));
        var stillCached = await client.GetAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromMinutes(8));
        var refreshed = await client.GetAsync(CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(stillCached);
        Assert.NotNull(refreshed);
        Assert.Equal("Stormchasers", stillCached!.Guild!.Name);
        Assert.Equal("Stormchasers Reborn", refreshed!.Guild!.Name);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAsync_returns_null_on_HttpRequestException()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

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
    public async Task UpdateAsync_patches_and_returns_dto_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn")));
        var request = new UpdateGuildRequest(
            Timezone: "Europe/Helsinki",
            Locale: "fi",
            Slogan: "New slogan",
            RankPermissions: null);

        var result = await client.UpdateAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Stormchasers Reborn", result!.Guild!.Name);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/guild", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "x", null);

        var result = await client.UpdateAsync(request, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_sends_etag_captured_from_get()
    {
        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"guild-etag-v1\"");
        responses.Enqueue(getResponse);
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "slogan", null);

        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(request, CancellationToken.None);

        Assert.True(requests[1].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"guild-etag-v1\"", ifMatch!.Single());
    }

    [Fact]
    public async Task UpdateAsync_replaces_cached_etag_from_patch_response()
    {
        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"guild-etag-v1\"");
        responses.Enqueue(getResponse);
        var firstPatchResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
        firstPatchResponse.Headers.TryAddWithoutValidation("ETag", "\"guild-etag-v2\"");
        responses.Enqueue(firstPatchResponse);
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "slogan", null);

        await client.GetAsync(CancellationToken.None);
        await client.UpdateAsync(request, CancellationToken.None);
        await client.UpdateAsync(request, CancellationToken.None);

        Assert.True(requests[2].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"guild-etag-v2\"", ifMatch!.Single());
    }

    // ── Admin GET/PATCH ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminAsync_calls_admin_url_and_returns_guild_dto()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto()));

        var result = await client.GetAdminAsync("guild 99", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Stormchasers", result!.Guild!.Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/guild/admin?guildId=guild%2099", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task UpdateAdminAsync_calls_patch_admin_url_with_json_body_and_returns_guild_dto()
    {
        string? body = null;
        var (client, handler) = MakeClient(new StubHttpMessageHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn"));
        }));
        var request = new UpdateGuildRequest(
            Timezone: "Europe/London",
            Locale: "en-gb",
            Slogan: "Admin slogan",
            RankPermissions: null);

        var result = await client.UpdateAdminAsync("guild 99", request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Stormchasers Reborn", result!.Guild!.Name);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/guild/admin?guildId=guild%2099", handler.LastRequest.RequestUri!.PathAndQuery);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("\"timezone\":\"Europe/London\"", body);
        Assert.Contains("\"locale\":\"en-gb\"", body);
        Assert.Contains("\"slogan\":\"Admin slogan\"", body);
    }

    [Fact]
    public async Task UpdateAdminAsync_sends_etag_captured_from_admin_get_for_same_guild()
    {
        var requests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpResponseMessage>();
        var getResponse = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
        getResponse.Headers.TryAddWithoutValidation("ETag", "\"admin-guild-etag-v1\"");
        responses.Enqueue(getResponse);
        responses.Enqueue(StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            return responses.Dequeue();
        }));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "slogan", null);

        await client.GetAdminAsync("99", CancellationToken.None);
        await client.UpdateAdminAsync("99", request, CancellationToken.None);

        Assert.True(requests[1].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"admin-guild-etag-v1\"", ifMatch!.Single());
    }

    [Fact]
    public async Task UpdateAdminAsync_does_not_keep_unbounded_admin_etag_cache()
    {
        var requests = new List<HttpRequestMessage>();
        var (client, _) = MakeClient(new StubHttpMessageHandler(request =>
        {
            requests.Add(request);
            if (request.Method == HttpMethod.Get)
            {
                var guildId = request.RequestUri!.Query.Split('=', 2)[1];
                var response = StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
                response.Headers.TryAddWithoutValidation("ETag", $"\"admin-guild-etag-{guildId}\"");
                return response;
            }

            return StubHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, MakeGuildDto("Stormchasers"));
        }));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "slogan", null);

        for (var i = 1; i <= 33; i++)
            await client.GetAdminAsync(i.ToString(), CancellationToken.None);
        await client.UpdateAdminAsync("1", request, CancellationToken.None);
        await client.UpdateAdminAsync("33", request, CancellationToken.None);

        Assert.False(requests[^2].Headers.TryGetValues("If-Match", out _));
        Assert.True(requests[^1].Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("\"admin-guild-etag-33\"", ifMatch!.Single());
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan offset) => _utcNow += offset;
    }
}
