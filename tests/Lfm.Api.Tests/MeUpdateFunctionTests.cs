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

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
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
        repo.Setup(r => r.UpsertAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var fn = new MeUpdateFunction(repo.Object);
        var ctx = MakeFunctionContext(principal);
        var req = MakeRequest(new { locale = "fi" });

        var result = await fn.Run(req, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UpdateMeResponse>(ok.Value);
        Assert.Equal("fi", response.Locale);

        repo.Verify(r => r.UpsertAsync(
            It.Is<RaiderDocument>(d => d.Locale == "fi" && d.BattleNetId == "bnet-1" && d.Ttl == 180 * 86400),
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

}
