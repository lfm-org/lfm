// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
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

public class WowReferenceRefreshFunctionTests
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

    /// <summary>
    /// Captures the NDJSON body streamed by the function into a fresh
    /// <see cref="DefaultHttpContext"/> and parses each line back into a
    /// <see cref="JsonElement"/>. Keeps the streaming behaviour under test
    /// without a live HTTP server.
    /// </summary>
    private static async Task<(HttpContext Context, IReadOnlyList<JsonElement> Lines)> InvokeAndReadBody(
        WowReferenceRefreshFunction fn, FunctionContext ctx)
    {
        var http = new DefaultHttpContext();
        using var body = new MemoryStream();
        http.Response.Body = body;

        var result = await fn.Run(http.Request, ctx, CancellationToken.None);
        Assert.IsType<EmptyResult>(result);

        body.Position = 0;
        using var reader = new StreamReader(body);
        var lines = new List<JsonElement>();
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            lines.Add(doc.RootElement.Clone());
        }
        return (http, lines);
    }

    private static JsonElement RequireDoneLine(IReadOnlyList<JsonElement> lines)
    {
        var done = lines.LastOrDefault(l => l.GetProperty("type").GetString() == "done");
        Assert.True(done.ValueKind == JsonValueKind.Object, "expected a terminal 'done' envelope");
        return done;
    }

    // ---------------------------------------------------------------------------
    // Test 1: Happy path — admin caller, all entities synced, streamed NDJSON
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Streams_done_envelope_with_sync_results_when_caller_is_site_admin()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var expectedResponse = new WowReferenceRefreshResponse(
        [
            new WowReferenceRefreshEntityResult("instances", "synced (12 docs)"),
            new WowReferenceRefreshEntityResult("specializations", "synced (36 docs)"),
        ]);

        var referenceSync = new Mock<IReferenceSync>();
        referenceSync
            .Setup(r => r.SyncAllAsync(It.IsAny<CancellationToken>(), It.IsAny<IProgress<WowReferenceRefreshProgress>?>()))
            .ReturnsAsync(expectedResponse);

        var fn = new WowReferenceRefreshFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var (http, lines) = await InvokeAndReadBody(fn, ctx);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal("application/x-ndjson", http.Response.ContentType);

        var done = RequireDoneLine(lines);
        var results = done.GetProperty("response").GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("instances", results[0].GetProperty("name").GetString());
        Assert.Equal("synced (12 docs)", results[0].GetProperty("status").GetString());

        referenceSync.Verify(
            r => r.SyncAllAsync(It.IsAny<CancellationToken>(), It.IsAny<IProgress<WowReferenceRefreshProgress>?>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Test 2: Admin-only gate — non-admin caller returns 403 (no stream)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Returns_403_when_caller_is_not_site_admin()
    {
        var principal = MakePrincipal("raider-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("raider-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var referenceSync = new Mock<IReferenceSync>();

        var fn = new WowReferenceRefreshFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);

        // SyncAllAsync must NOT be called for non-admin callers.
        referenceSync.Verify(
            r => r.SyncAllAsync(It.IsAny<CancellationToken>(), It.IsAny<IProgress<WowReferenceRefreshProgress>?>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // Test 3: Progress events are streamed before the terminal 'done' line
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Streams_progress_events_before_done_envelope()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var finalResponse = new WowReferenceRefreshResponse(
        [
            new WowReferenceRefreshEntityResult("instances", "synced (2 docs)"),
        ]);

        var referenceSync = new Mock<IReferenceSync>();
        referenceSync
            .Setup(r => r.SyncAllAsync(It.IsAny<CancellationToken>(), It.IsAny<IProgress<WowReferenceRefreshProgress>?>()))
            .Returns<CancellationToken, IProgress<WowReferenceRefreshProgress>?>((_, progress) =>
            {
                progress?.Report(new WowReferenceRefreshProgress("instances", "start", 0, 2));
                progress?.Report(new WowReferenceRefreshProgress("instances", "progress", 1, 2, Current: "Ara-Kara"));
                progress?.Report(new WowReferenceRefreshProgress("instances", "progress", 2, 2, Current: "Mists"));
                return Task.FromResult(finalResponse);
            });

        var fn = new WowReferenceRefreshFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var (_, lines) = await InvokeAndReadBody(fn, ctx);

        var progressLines = lines
            .Where(l => l.GetProperty("type").GetString() == "progress")
            .ToList();
        Assert.Equal(3, progressLines.Count);
        Assert.Equal("start", progressLines[0].GetProperty("phase").GetString());
        Assert.Equal("Ara-Kara", progressLines[1].GetProperty("current").GetString());

        // 'done' must be the last line.
        Assert.Equal("done", lines[^1].GetProperty("type").GetString());
    }

    // ---------------------------------------------------------------------------
    // Test 4: Partial failure — response body still carries both rows
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Streams_done_envelope_with_partial_results_when_one_entity_fails()
    {
        var principal = MakePrincipal("admin-1");

        var siteAdmin = new Mock<ISiteAdminService>();
        siteAdmin.Setup(s => s.IsAdminAsync("admin-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var partialResponse = new WowReferenceRefreshResponse(
        [
            new WowReferenceRefreshEntityResult("instances", "failed: Blizzard API returned 503"),
            new WowReferenceRefreshEntityResult("specializations", "synced (36 docs)"),
        ]);

        var referenceSync = new Mock<IReferenceSync>();
        referenceSync
            .Setup(r => r.SyncAllAsync(It.IsAny<CancellationToken>(), It.IsAny<IProgress<WowReferenceRefreshProgress>?>()))
            .ReturnsAsync(partialResponse);

        var fn = new WowReferenceRefreshFunction(siteAdmin.Object, referenceSync.Object);
        var ctx = MakeFunctionContext(principal);

        var (http, lines) = await InvokeAndReadBody(fn, ctx);

        // Even with partial failure the HTTP response is 200 — failures are in the body.
        Assert.Equal(200, http.Response.StatusCode);

        var done = RequireDoneLine(lines);
        var results = done.GetProperty("response").GetProperty("results");
        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("failed: Blizzard API returned 503", results[0].GetProperty("status").GetString());
    }
}
