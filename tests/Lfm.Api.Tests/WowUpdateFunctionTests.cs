// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Services;
using Lfm.Contracts.Admin;
using Xunit;

namespace Lfm.Api.Tests;

public class WowUpdateFunctionTests
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

    // ---------------------------------------------------------------------------
    // Test 1: Happy path — admin caller, all entities synced
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_200_with_sync_results_when_caller_is_site_admin()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expectedResponse = new WowUpdateResponse(
        [
            new WowUpdateEntityResult("instances", "synced (12 docs)"),
            new WowUpdateEntityResult("specializations", "synced (36 docs)"),
        ]);

        var referenceSync = new Mock<IReferenceSync>();
        referenceSync.Setup(r => r.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var fn = new WowUpdateFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedResponse, ok.Value);

        referenceSync.Verify(r => r.SyncAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Test 2: Admin-only gate — non-admin caller returns 403
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_403_when_caller_is_not_site_admin()
    {
        var principal = MakePrincipal("raider-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("raider-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var referenceSync = new Mock<IReferenceSync>();

        var fn = new WowUpdateFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);

        // SyncAllAsync must NOT be called for non-admin callers.
        referenceSync.Verify(r => r.SyncAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Test 3: Partial failure — one entity fails, others succeed; still returns 200
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_200_with_partial_results_when_one_entity_fails()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // instances fails, specializations succeeds
        var partialResponse = new WowUpdateResponse(
        [
            new WowUpdateEntityResult("instances", "failed: Blizzard API returned 503"),
            new WowUpdateEntityResult("specializations", "synced (36 docs)"),
        ]);

        var referenceSync = new Mock<IReferenceSync>();
        referenceSync.Setup(r => r.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(partialResponse);

        var fn = new WowUpdateFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        // Even with partial failure the HTTP response is 200 — failures are in the body.
        // Assert against the same WowUpdateResponse instance the stub returned, not pasted literals.
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(partialResponse, ok.Value);
    }

}
