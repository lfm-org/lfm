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

public class GuildFunctionTests
{
    // Convenience: build a FunctionContext that returns the given principal.
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static HttpRequest MakeGetRequest()
        => new DefaultHttpContext().Request;

    private static HttpRequest MakePatchRequest(object body, string? ifMatch = null)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        if (ifMatch is not null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return httpContext.Request;
    }

    private static SessionPrincipal MakePrincipal(string guildId = "12345") =>
        new SessionPrincipal(
            BattleNetId: "bnet-1",
            BattleTag: "Player#1234",
            GuildId: guildId,
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaiderDoc(string battleNetId = "bnet-1") =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "test-realm",
                    Name: "Testchar",
                    GuildId: 12345,
                    GuildName: "Test Guild")
            ]);

    private static GuildDocument MakeGuildDoc(string id = "12345") =>
        new GuildDocument(
            Id: id,
            GuildId: 12345,
            RealmSlug: "test-realm",
            Slogan: "We raid hard",
            Setup: new GuildSetup(
                InitializedAt: "2025-01-01T00:00:00Z",
                Timezone: "Europe/Helsinki",
                Locale: "fi"));

    private static GuildFunction MakeFunction(
        Mock<IGuildRepository>? guildRepo = null,
        Mock<IRaidersRepository>? raidersRepo = null,
        Mock<IGuildPermissions>? permissions = null,
        TestLogger<GuildFunction>? logger = null)
    {
        return new GuildFunction(
            (guildRepo ?? new Mock<IGuildRepository>()).Object,
            (raidersRepo ?? new Mock<IRaidersRepository>()).Object,
            (permissions ?? new Mock<IGuildPermissions>()).Object,
            logger ?? new TestLogger<GuildFunction>());
    }

    // ------------------------------------------------------------------
    // Test 1: GET happy path
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildGet_returns_guild_dto_when_guild_doc_exists()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc();
        var raiderDoc = MakeRaiderDoc();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var permissions = new Mock<IGuildPermissions>();

        var logger = new TestLogger<GuildFunction>();
        var fn = MakeFunction(guildRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.GuildGet(MakeGetRequest(), ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<GuildDto>(ok.Value);
        Assert.Equal("fi", dto.Setup.Locale);
        Assert.Equal("Europe/Helsinki", dto.Setup.Timezone);
        Assert.True(dto.Setup.IsInitialized);
    }

    // ------------------------------------------------------------------
    // Test 2: GET returns 404 when raider document is missing
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildGet_returns_404_when_raider_not_found()
    {
        var principal = MakePrincipal();

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var fn = MakeFunction(raidersRepo: raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.GuildGet(MakeGetRequest(), ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#raider-not-found", problem.Type);
        Assert.Equal("Raider not found.", problem.Detail);
    }

    // ------------------------------------------------------------------
    // Test 3: GET returns no-guild DTO when selected character has no guild
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildGet_returns_no_guild_dto_when_selected_character_has_no_guild()
    {
        var principal = MakePrincipal(guildId: null!);

        // Raider has a selected character but the character has no guild → FromRaider returns (null, null).
        var raiderDoc = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "test-realm",
                    Name: "Testchar")
            ]);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var fn = MakeFunction(raidersRepo: raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.GuildGet(MakeGetRequest(), ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<GuildDto>(ok.Value);
        Assert.Null(dto.Guild);
        Assert.False(dto.Setup.IsInitialized);
        Assert.True(dto.Setup.RequiresSetup);
    }

    // ------------------------------------------------------------------
    // Test 3: PATCH admin success (200)
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildUpdate_returns_ok_when_caller_is_admin()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc();
        var raiderDoc = MakeRaiderDoc();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var logger = new TestLogger<GuildFunction>();
        var fn = MakeFunction(guildRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new
        {
            timezone = "Europe/London",
            locale = "en-gb",
            slogan = "Updated slogan",
        });

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<GuildDto>(ok.Value);

        guildRepo.Verify(r => r.UpsertAsync(
            It.Is<GuildDocument>(d => d.Setup!.Timezone == "Europe/London" && d.Setup.Locale == "en-gb"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 4: PATCH non-admin (403)
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildUpdate_returns_403_when_caller_is_not_admin()
    {
        var principal = MakePrincipal();
        var raiderDoc = MakeRaiderDoc();

        var guildRepo = new Mock<IGuildRepository>();

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var logger = new TestLogger<GuildFunction>();
        var fn = MakeFunction(guildRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new { timezone = "Europe/London", locale = "en-gb" });

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(statusResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#guild-admin-only", problem.Type);

        // Guild document should never be read when caller is not admin.
        guildRepo.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Denied admin attempt must emit a failure audit event.
        Assert.Single(
            logger.Entries,
            e => e.IsAudit("guild.update", "failure", "forbidden"));
    }

    // -----------------------------------------------------------------------
    // Audit events
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GuildUpdate_emits_guild_update_audit_event()
    {
        // Arrange
        var principal = MakePrincipal("12345");
        var guildDoc = MakeGuildDoc();
        var raiderDoc = MakeRaiderDoc();
        var logger = new TestLogger<GuildFunction>();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(guildRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new
        {
            timezone = "Europe/London",
            locale = "en-gb",
            slogan = "Updated slogan",
        });

        // Act
        await fn.GuildUpdate(req, ctx, CancellationToken.None);

        // Assert: logger called with "guild.update" and "success"
        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "guild.update",
            actorId: "bnet-1",
            result: "success"));
    }

    // ------------------------------------------------------------------
    // XSS payload in slogan — the slogan is user-supplied free text. The
    // contract is that the API stores it verbatim and does not 500 on any
    // payload shape; XSS prevention belongs at the render layer (Blazor
    // encodes by default). Replaces the deleted SecuritySpec.cs E2E test
    // `XssPayload_InFormFields_Sanitized`, which exercised the full Docker
    // stack to prove the same property at a much higher cost.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildUpdate_persists_script_tag_slogan_verbatim_without_500()
    {
        const string XssSlogan = "<script>alert('xss')</script><img src=x onerror=alert(1)>";

        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc();
        var raiderDoc = MakeRaiderDoc();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        GuildDocument? captured = null;
        guildRepo.Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .Callback<GuildDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raiderDoc);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(guildRepo, raidersRepo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new
        {
            timezone = "Europe/London",
            locale = "en-gb",
            slogan = XssSlogan,
        });

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(XssSlogan, captured!.Slogan);
    }

    // ------------------------------------------------------------------
    // Tests 10-13: ETag / If-Match contract for GET + PATCH /api/guild
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildGet_emits_etag_header_mirroring_cosmos_etag()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc() with { ETag = "\"guild-etag-v1\"" };

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc());

        var fn = MakeFunction(guildRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);
        var req = MakeGetRequest();

        var result = await fn.GuildGet(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("\"guild-etag-v1\"", req.HttpContext.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task GuildUpdate_uses_ReplaceAsync_when_if_match_is_specific()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc() with { ETag = "\"guild-etag-v1\"" };
        var replaced = guildDoc with { ETag = "\"guild-etag-v2\"" };

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), "\"guild-etag-v1\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(replaced);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc());

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(guildRepo, raidersRepo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new { timezone = "Europe/London", locale = "en-gb" }, ifMatch: "\"guild-etag-v1\"");

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("\"guild-etag-v2\"", req.HttpContext.Response.Headers.ETag.ToString());
        guildRepo.Verify(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        guildRepo.Verify(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), "\"guild-etag-v1\"", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GuildUpdate_returns_412_when_if_match_is_stale()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc() with { ETag = "\"guild-etag-v1\"" };
        var logger = new TestLogger<GuildFunction>();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc());

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(guildRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new { timezone = "Europe/London", locale = "en-gb" }, ifMatch: "\"stale\"");

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(412, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#if-match-stale", problem.Type);

        Assert.Single(logger.Entries, e => e.IsAudit("guild.update", "failure", "if-match stale"));
    }

    [Fact]
    public async Task GuildUpdate_wildcard_if_match_falls_back_to_blind_upsert()
    {
        var principal = MakePrincipal();
        var guildDoc = MakeGuildDoc();

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);
        guildRepo.Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc());

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.IsAdminAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(guildRepo, raidersRepo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = MakePatchRequest(new { timezone = "Europe/London", locale = "en-gb" }, ifMatch: "*");

        var result = await fn.GuildUpdate(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        guildRepo.Verify(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        guildRepo.Verify(r => r.ReplaceAsync(It.IsAny<GuildDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
