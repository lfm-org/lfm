using System.Text;
using System.Text.Json;
using FluentAssertions;
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

    private static HttpRequest MakePatchRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
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

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo.Setup(r => r.GetAsync("12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guildDoc);

        var permissions = new Mock<IGuildPermissions>();

        var logger = new TestLogger<GuildFunction>();
        var fn = MakeFunction(guildRepo, permissions: permissions, logger: logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.GuildGet(MakeGetRequest(), ctx, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<GuildDto>().Subject;
        dto.Setup.Locale.Should().Be("fi");
        dto.Setup.Timezone.Should().Be("Europe/Helsinki");
        dto.Setup.IsInitialized.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Test 2: GET returns no-guild DTO when principal has no guild
    // ------------------------------------------------------------------

    [Fact]
    public async Task GuildGet_returns_no_guild_dto_when_principal_has_no_guild_id()
    {
        var principal = MakePrincipal(guildId: null!);
        var fn = MakeFunction();
        var ctx = MakeFunctionContext(principal);

        var result = await fn.GuildGet(MakeGetRequest(), ctx, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<GuildDto>().Subject;
        dto.Guild.Should().BeNull();
        dto.Setup.IsInitialized.Should().BeFalse();
        dto.Setup.RequiresSetup.Should().BeTrue();
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

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<GuildDto>();

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

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(403);

        // Guild document should never be read when caller is not admin.
        guildRepo.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Denied admin attempt must emit a failure audit event.
        logger.Entries.Should().ContainSingle(
            e => e.IsAudit("guild.update", "failure", "forbidden"),
            "denied admin guild update must emit a failure audit event");
    }

    // ------------------------------------------------------------------
    // Test 5: [RequireAuth] attribute present on both methods
    // ------------------------------------------------------------------

    [Fact]
    public void GuildGet_and_GuildUpdate_have_RequireAuth_attribute()
    {
        var getMethod = typeof(GuildFunction).GetMethod(nameof(GuildFunction.GuildGet));
        getMethod.Should().NotBeNull();
        getMethod!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "GuildFunction.GuildGet must carry [RequireAuth]");

        var patchMethod = typeof(GuildFunction).GetMethod(nameof(GuildFunction.GuildUpdate));
        patchMethod.Should().NotBeNull();
        patchMethod!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "GuildFunction.GuildUpdate must carry [RequireAuth]");
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
        logger.Entries.Should().ContainSingle(e => e.IsAudit(
            action: "guild.update",
            actorId: "bnet-1",
            result: "success"),
            "guild update must emit an audit event with action=guild.update and result=success");
    }
}
