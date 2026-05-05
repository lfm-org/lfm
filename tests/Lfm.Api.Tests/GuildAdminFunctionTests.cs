// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Guild;
using Xunit;

namespace Lfm.Api.Tests;

public class GuildAdminFunctionTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "admin-1") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Admin#0001",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static GuildDocument MakeGuildDoc(string id = "99") =>
        new GuildDocument(
            Id: id,
            GuildId: 99,
            RealmSlug: "test-realm",
            Slogan: "Admin view",
            RankPermissions: new[] { new GuildRankPermission(0, true, true, true) },
            Setup: new GuildSetup(
                InitializedAt: "2025-01-01T00:00:00Z",
                Timezone: "Europe/Helsinki",
                Locale: "fi"));

    private static HttpRequest MakeRequest(string? guildId)
    {
        var httpContext = new DefaultHttpContext();
        if (guildId is not null)
            httpContext.Request.QueryString = new QueryString($"?guildId={guildId}");
        return httpContext.Request;
    }

    private static HttpRequest MakePatchRequest(string? guildId, object body, string? ifMatch = null)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Patch;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        if (guildId is not null)
            httpContext.Request.QueryString = new QueryString($"?guildId={guildId}");
        if (ifMatch is not null)
            httpContext.Request.Headers.IfMatch = ifMatch;
        return httpContext.Request;
    }

    // ---------------------------------------------------------------------------
    // Test 1: Non-admin caller gets 403
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_403_when_caller_is_not_site_admin()
    {
        var principal = MakePrincipal("raider-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("raider-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var guildRepo = new Mock<IGuildRepository>();

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeRequest("99"), ctx, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(forbidden.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#admin-only", problem.Type);

        guildRepo.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Test 2: Missing guildId query parameter gets 400
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_400_when_guildId_query_parameter_is_missing()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guildRepo = new Mock<IGuildRepository>();

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeRequest(null), ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#missing-parameter", problem.Type);
    }

    // ---------------------------------------------------------------------------
    // Test 3: Admin gets guild data
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_guild_dto_when_caller_is_admin_and_guild_exists()
    {
        var principal = MakePrincipal("admin-1");
        var guildDoc = MakeGuildDoc("99");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeRequest("99"), ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<GuildDto>(ok.Value);
        Assert.Equal("fi", dto.Setup.Locale);
        Assert.Equal("Europe/Helsinki", dto.Setup.Timezone);
        Assert.True(dto.Editor.CanEdit);
        Assert.NotNull(dto.Settings);
        Assert.False(dto.MemberPermissions.CanCreateGuildRuns);
        Assert.False(dto.MemberPermissions.CanSignupGuildRuns);
        Assert.False(dto.MemberPermissions.CanDeleteGuildRuns);
    }

    // ---------------------------------------------------------------------------
    // Test 4: Admin with valid guildId but guild not found gets 404
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_404_when_guild_does_not_exist()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDocument?)null);

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeRequest("nonexistent"), ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#guild-not-found", problem.Type);
    }

    [Fact]
    public async Task PatchV1_Updates_ArbitraryGuild_WhenCallerIsSiteAdmin()
    {
        var principal = MakePrincipal("admin-1");
        var guildDoc = MakeGuildDoc("99") with
        {
            Setup = null,
            ETag = "\"guild-etag-v1\"",
        };
        GuildDocument? captured = null;

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), "\"guild-etag-v1\"", It.IsAny<CancellationToken>()))
            .Callback<GuildDocument, string, CancellationToken>((doc, _, _) => captured = doc)
            .ReturnsAsync((GuildDocument doc, string _, CancellationToken _) => doc with { ETag = "\"guild-etag-v2\"" });

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest("99", new
        {
            timezone = "Europe/London",
            locale = "en-gb",
            slogan = "Updated by site admin",
            rankPermissions = new[]
            {
                new { rank = 0, canCreateGuildRuns = true, canSignupGuildRuns = true, canDeleteGuildRuns = true },
                new { rank = 5, canCreateGuildRuns = false, canSignupGuildRuns = true, canDeleteGuildRuns = false },
            },
        }, "\"guild-etag-v1\"");

        var result = await fn.RunV1(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<GuildDto>(ok.Value);
        Assert.NotNull(captured);
        Assert.Equal("99", captured!.Id);
        Assert.Equal("Europe/London", captured.Setup!.Timezone);
        Assert.Equal("en-gb", captured.Setup.Locale);
        Assert.False(string.IsNullOrWhiteSpace(captured.Setup.InitializedAt));
        Assert.Equal("Updated by site admin", captured.Slogan);
        Assert.Equal(2, captured.RankPermissions?.Count);
        Assert.Equal("\"guild-etag-v2\"", req.HttpContext.Response.Headers.ETag.ToString());
        guildRepo.Verify(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PatchV1_Returns403_WhenCallerIsNotSiteAdmin()
    {
        var principal = MakePrincipal("raider-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("raider-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var guildRepo = new Mock<IGuildRepository>();

        var fn = new GuildAdminFunction(guildRepo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest("99", new
        {
            timezone = "Europe/London",
            locale = "en-gb",
        }, "\"guild-etag-v1\"");

        var result = await fn.RunV1(req, ctx, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        guildRepo.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        guildRepo.Verify(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        guildRepo.Verify(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
