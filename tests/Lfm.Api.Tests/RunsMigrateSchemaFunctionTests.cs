// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsMigrateSchemaFunctionTests
{
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "admin-1") =>
        new(
            BattleNetId: battleNetId,
            BattleTag: "Admin#0001",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task Returns_200_with_migration_result_for_site_admin()
    {
        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var runs = new Mock<IRunsRepository>();
        runs.Setup(r => r.MigrateSchemaAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunMigrationResult(Scanned: 12, Migrated: 7, DryRun: false));

        var fn = new RunsMigrateSchemaFunction(siteAdmin.Object, runs.Object, NullLogger<RunsMigrateSchemaFunction>.Instance);
        var result = await fn.Run(new DefaultHttpContext().Request, MakeFunctionContext(MakePrincipal()), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<RunMigrationResult>(ok.Value);
        Assert.Equal(12, body.Scanned);
        Assert.Equal(7, body.Migrated);
        Assert.False(body.DryRun);
    }

    [Fact]
    public async Task Returns_403_for_non_admin_and_does_not_call_repository()
    {
        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var runs = new Mock<IRunsRepository>();

        var fn = new RunsMigrateSchemaFunction(siteAdmin.Object, runs.Object, NullLogger<RunsMigrateSchemaFunction>.Instance);
        var result = await fn.Run(new DefaultHttpContext().Request, MakeFunctionContext(MakePrincipal("raider-1")), CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(forbidden.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#admin-only", problem.Type);
        runs.Verify(r => r.MigrateSchemaAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DryRun_query_flag_propagates_to_repository()
    {
        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var runs = new Mock<IRunsRepository>();
        runs.Setup(r => r.MigrateSchemaAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunMigrationResult(Scanned: 12, Migrated: 7, DryRun: true));

        var fn = new RunsMigrateSchemaFunction(siteAdmin.Object, runs.Object, NullLogger<RunsMigrateSchemaFunction>.Instance);

        var req = new DefaultHttpContext().Request;
        req.QueryString = new QueryString("?dryRun=true");
        var result = await fn.Run(req, MakeFunctionContext(MakePrincipal()), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<RunMigrationResult>(ok.Value);
        Assert.True(body.DryRun);
        runs.Verify(r => r.MigrateSchemaAsync(true, It.IsAny<CancellationToken>()), Times.Once);
        runs.Verify(r => r.MigrateSchemaAsync(false, It.IsAny<CancellationToken>()), Times.Never);
    }
}
