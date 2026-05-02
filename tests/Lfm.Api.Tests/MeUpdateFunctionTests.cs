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
using Lfm.Contracts.Me;
using Xunit;

namespace Lfm.Api.Tests;

public class MeUpdateFunctionTests
{
    // Mirrors the helper in MeFunctionTests.
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static HttpRequest MakeRequest(object body, string? ifMatch = null)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        if (ifMatch is not null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return httpContext.Request;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Returns_updated_locale_when_raider_exists_and_body_is_valid()
    {
        var principal = MakePrincipal();
        var existing = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), "\"etag-v1\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing with { Locale = "fi", ETag = "\"etag-v2\"" });

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" }, ifMatch: "\"etag-v1\"");

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UpdateMeResponse>(ok.Value);
        Assert.Equal("fi", response.Locale);

        repo.Verify(r => r.ReplaceAsync(
            It.Is<RaiderDocument>(d => d.Locale == "fi" && d.BattleNetId == "bnet-1" && d.Ttl == 180 * 86400),
            "\"etag-v1\"",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_bad_request_when_locale_is_invalid()
    {
        var principal = MakePrincipal();

        var repo = new Mock<IRaidersRepository>();

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "de" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#validation-failed", problem.Type);
        repo.Verify(r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Uses_ReplaceAsync_and_echoes_etag_when_if_match_is_specific()
    {
        var principal = MakePrincipal();
        var existing = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en",
            ETag: "\"etag-v1\"");
        var replaced = existing with { Locale = "fi", ETag = "\"etag-v2\"" };

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), "\"etag-v1\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(replaced);

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" }, ifMatch: "\"etag-v1\"");

        var result = await fn.Run(req, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("\"etag-v2\"", req.HttpContext.Response.Headers.ETag.ToString());

        repo.Verify(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), "\"etag-v1\"", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Returns_412_when_if_match_is_stale()
    {
        var principal = MakePrincipal();
        var existing = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en",
            ETag: "\"etag-v1\"");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" }, ifMatch: "\"etag-stale\"");

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(412, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#if-match-stale", problem.Type);
    }

    [Fact]
    public async Task Missing_if_match_returns_428()
    {
        var principal = MakePrincipal();
        var existing = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(428, objectResult.StatusCode);
        repo.Verify(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Wildcard_if_match_returns_428()
    {
        var principal = MakePrincipal();
        var existing = new RaiderDocument(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: null,
            Locale: "en");

        var repo = new Mock<IRaidersRepository>();
        repo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" }, ifMatch: "*");

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(428, objectResult.StatusCode);
        repo.Verify(r => r.ReplaceAsync(It.IsAny<RaiderDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
