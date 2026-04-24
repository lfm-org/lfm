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
    private static (GuildClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new GuildClient(factory.Object), handler);
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
        Assert.Equal("/api/guild", handler.LastRequest.RequestUri!.PathAndQuery);
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
        Assert.Equal("/api/guild", handler.LastRequest.RequestUri!.PathAndQuery);
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
    public async Task UpdateAsync_sends_wildcard_if_match_header()
    {
        // Contract: the API requires callers to declare awareness of the
        // ETag contract. Until this client round-trips the server-issued
        // ETag, it sends `*` to claim the current server state without
        // checking a specific version.
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto("Stormchasers")));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "slogan", null);

        await client.UpdateAsync(request, CancellationToken.None);

        Assert.True(handler.LastRequest!.Headers.TryGetValues("If-Match", out var ifMatch));
        Assert.Equal("*", ifMatch!.Single());
    }
}
