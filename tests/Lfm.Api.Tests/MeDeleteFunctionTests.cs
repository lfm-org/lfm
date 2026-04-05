using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class MeDeleteFunctionTests
{
    // Mirrors the helper in MeFunctionTests.
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
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
    public async Task Returns_ok_and_calls_both_repos_in_order_when_raider_exists()
    {
        var principal = MakePrincipal("bnet-1");

        var callOrder = new List<string>();

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) => callOrder.Add("scrub"))
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);

        var fn = new MeDeleteFunction(runsRepo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>("TS handler returns status 200 with { deleted: true }");

        runsRepo.Verify(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
        raidersRepo.Verify(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
        callOrder.Should().Equal(["scrub", "delete"], "runs must be scrubbed before the raider document is deleted");
    }

    // Verify that the [RequireAuth] attribute is present on the Run method.
    // AuthPolicyMiddleware enforces the 401 at the framework level based on this attribute;
    // no unit test is needed for the 401 path itself.
    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(MeDeleteFunction).GetMethod(nameof(MeDeleteFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "MeDeleteFunction.Run must carry [RequireAuth] for AuthPolicyMiddleware to enforce 401");
    }
}
