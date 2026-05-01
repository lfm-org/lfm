// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Runs;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsSignupOptionsFunctionTests
{
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-user") =>
        new(
            BattleNetId: battleNetId,
            BattleTag: "User#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Run_returns_ok_with_signup_options()
    {
        var service = new Mock<IRunSignupOptionsService>();
        service.Setup(s => s.GetAsync("run-1", It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSignupOptionsResult.Ok(new RunSignupOptionsDto([
                new CharacterDto(
                    Name: "Guildmain",
                    Realm: "silvermoon",
                    RealmName: "Silvermoon",
                    Level: 80,
                    Region: "eu")
            ])));
        var fn = new RunsSignupOptionsFunction(service.Object);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-1",
            MakeFunctionContext(MakePrincipal()),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var options = Assert.IsType<RunSignupOptionsDto>(ok.Value);
        Assert.Single(options.Characters);
    }

    [Fact]
    public async Task Run_returns_no_content_when_account_characters_need_refresh()
    {
        var service = new Mock<IRunSignupOptionsService>();
        service.Setup(s => s.GetAsync("run-1", It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSignupOptionsResult.NeedsRefresh());
        var fn = new RunsSignupOptionsFunction(service.Object);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-1",
            MakeFunctionContext(MakePrincipal()),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Run_returns_problem_for_forbidden()
    {
        var service = new Mock<IRunSignupOptionsService>();
        service.Setup(s => s.GetAsync("run-1", It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunSignupOptionsResult.Forbidden(
                "guild-rank-denied",
                "Guild signup is not enabled for your rank."));
        var fn = new RunsSignupOptionsFunction(service.Object);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-1",
            MakeFunctionContext(MakePrincipal()),
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#guild-rank-denied", details.Type);
    }
}
