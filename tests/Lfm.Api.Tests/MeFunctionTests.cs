// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Me;
using Xunit;

namespace Lfm.Api.Tests;

public class MeFunctionTests
{
    // Convenience: build a FunctionContext that returns the given principal from GetPrincipal().
    // GetPrincipal() reads context.Items[SessionKeys.Principal], so we stub Items accordingly.
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    [Fact]
    public async Task Returns_me_response_when_raider_exists()
    {
        var principal = new SessionPrincipal(
            BattleNetId: "bnet-1",
            BattleTag: "Player#1234",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var raider = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: "char-1",
            Locale: "fi",
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Testchar",
                    GuildId: 42,
                    GuildName: "Test Guild")
            ]);

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = new MeFunction(repo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var me = Assert.IsType<MeResponse>(ok.Value);
        Assert.Equal("bnet-1", me.BattleNetId);
        Assert.Equal("Test Guild", me.GuildName);
        Assert.Equal("char-1", me.SelectedCharacterId);
        Assert.False(me.IsSiteAdmin);
        Assert.Equal("fi", me.Locale);
    }

    [Fact]
    public async Task Returns_not_found_when_raider_document_missing()
    {
        var principal = new SessionPrincipal(
            BattleNetId: "bnet-unknown",
            BattleTag: "Ghost#0000",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var siteAdmin = new Mock<ISiteAdminService>();

        var fn = new MeFunction(repo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#raider-not-found", problem.Type);
        siteAdmin.Verify(s => s.IsAdminAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunV1_delegates_to_canonical_Run_handler()
    {
        // Alias contract: /api/v1/me is a thin delegation; if a future
        // refactor drifts the two paths this test fails fast.
        var principal = new SessionPrincipal(
            BattleNetId: "bnet-1",
            BattleTag: "Player#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var raider = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: "char-1",
            Locale: "en");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = new MeFunction(repo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.RunV1(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var me = Assert.IsType<MeResponse>(ok.Value);
        Assert.Equal("bnet-1", me.BattleNetId);
    }

    [Fact]
    public async Task Response_carries_etag_header_mirroring_cosmos_etag()
    {
        var principal = new SessionPrincipal(
            BattleNetId: "bnet-1",
            BattleTag: "Player#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var raider = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en",
            ETag: "\"cosmos-etag-abc\"");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = new MeFunction(repo.Object, siteAdmin.Object);
        var ctx = MakeFunctionContext(principal);

        var request = new DefaultHttpContext().Request;
        var result = await fn.Run(request, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("\"cosmos-etag-abc\"", request.HttpContext.Response.Headers.ETag.ToString());
    }
}
