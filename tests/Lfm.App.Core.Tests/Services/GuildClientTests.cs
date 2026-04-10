using System.Net;
using FluentAssertions;
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
                RealmSlug: "silvermoon",
                RealmName: "Silvermoon",
                FactionName: "Alliance",
                MemberCount: 120,
                AchievementPoints: 5000,
                SyncedMemberCount: 100,
                RankCount: 10,
                CrestEmblemUrl: null,
                CrestBorderUrl: null),
            Setup: new GuildSetupDto(
                IsInitialized: true,
                RequiresSetup: false,
                RankDataFresh: true,
                RankDataFetchedAt: null,
                Timezone: "Europe/Helsinki",
                Locale: "fi"),
            Settings: null,
            Editor: new GuildEditorDto(CanEdit: false, Mode: "member"),
            MemberPermissions: new GuildMemberPermissionsDto(
                MatchedRank: 3,
                CanCreateGuildRuns: true,
                CanSignupGuildRuns: true,
                CanDeleteGuildRuns: false,
                RankDataFresh: true));

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_returns_guild_dto_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto()));

        var result = await client.GetAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Guild!.Name.Should().Be("Stormchasers");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/guild");
    }

    [Fact]
    public async Task GetAsync_returns_null_on_HttpRequestException()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Throws(new HttpRequestException("network down")));

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
    public async Task UpdateAsync_patches_and_returns_dto_on_success()
    {
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, MakeGuildDto("Stormchasers Reborn")));
        var request = new UpdateGuildRequest(
            Timezone: "Europe/Helsinki",
            Locale: "fi",
            Slogan: "New slogan",
            RankPermissions: null);

        var result = await client.UpdateAsync(request, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Guild!.Name.Should().Be("Stormchasers Reborn");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/guild");
        handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task UpdateAsync_returns_null_on_non_success_status()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.Forbidden));
        var request = new UpdateGuildRequest("Europe/Helsinki", "fi", "x", null);

        var result = await client.UpdateAsync(request, CancellationToken.None);

        result.Should().BeNull();
    }
}
