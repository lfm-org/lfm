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
using Lfm.Api.Runs;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsUpdateFunctionTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-creator") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Creator#1234",
            GuildId: "12345",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private const string DefaultTestEtag = "\"test-etag\"";

    private static HttpRequest MakePutRequest(object body, string? ifMatch = DefaultTestEtag)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        if (ifMatch is not null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return httpContext.Request;
    }

    private static HttpRequest MakePutRequest(string rawJson, string? ifMatch = DefaultTestEtag)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        httpContext.Request.ContentType = "application/json";
        if (ifMatch is not null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return httpContext.Request;
    }

    private static RunDocument MakeUpdatedRun(
        string id = "run-1",
        string? etag = null) =>
        new RunDocument(
            Id: id,
            StartTime: DateTimeOffset.UtcNow.AddHours(24).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(22).ToString("o"),
            Description: "Updated description",
            ModeKey: "NORMAL:10",
            Visibility: "GUILD",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: [],
            Difficulty: "NORMAL",
            Size: 10,
            ETag: etag);

    private static RunsUpdateFunction MakeFunction(
        Mock<IRunUpdateService> service,
        TestLogger<RunsUpdateFunction>? logger = null)
    {
        return new RunsUpdateFunction(
            service.Object,
            logger ?? new TestLogger<RunsUpdateFunction>());
    }

    // ------------------------------------------------------------------
    // Test 1: Service Ok happy path → 200 OK with mapped DTO and audit
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_200_and_emits_success_audit_when_service_returns_ok()
    {
        var principal = MakePrincipal();
        var updated = MakeUpdatedRun();
        var logger = new TestLogger<RunsUpdateFunction>();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(updated));

        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Updated description" }), "run-1", ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<RunDetailDto>(ok.Value);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "run.update",
            actorId: "bnet-creator",
            result: "success"));
    }

    // ------------------------------------------------------------------
    // Test 2: Happy path echoes the persisted ETag back on the response
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_echoes_new_etag_on_successful_update()
    {
        var principal = MakePrincipal();
        var updated = MakeUpdatedRun(etag: "\"new-etag\"");

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(updated));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var request = MakePutRequest(new { description = "Updated description" });
        await fn.Run(request, "run-1", ctx, CancellationToken.None);

        Assert.Equal("\"new-etag\"", request.HttpContext.Response.Headers.ETag.ToString());
    }

    // ------------------------------------------------------------------
    // Test 3: Service receives the client's If-Match header verbatim
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_forwards_client_if_match_header_to_service()
    {
        var principal = MakePrincipal();
        var updated = MakeUpdatedRun();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                "\"client-etag\"",
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(updated));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest(new { description = "Updated description" }, ifMatch: "\"client-etag\""),
            "run-1",
            ctx,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(s => s.UpdateAsync(
            "run-1",
            It.IsAny<UpdateRunRequest>(),
            It.IsAny<RunUpdatePresentFields>(),
            "\"client-etag\"",
            It.IsAny<SessionPrincipal>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 4: Validation failure — returns 400 with error details
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_400_when_update_start_time_is_invalid()
    {
        var principal = MakePrincipal();
        var service = new Mock<IRunUpdateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest(new { startTime = "not-a-date" }),
            "run-1",
            ctx,
            CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#validation-failed", problem.Type);

        // Service should never be called when validation fails.
        service.Verify(s => s.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<UpdateRunRequest>(),
            It.IsAny<RunUpdatePresentFields>(),
            It.IsAny<string>(),
            It.IsAny<SessionPrincipal>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_returns_400_when_update_start_time_is_whitespace()
    {
        var principal = MakePrincipal();
        var service = new Mock<IRunUpdateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest(new { startTime = "   " }),
            "run-1",
            ctx,
            CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#validation-failed", problem.Type);
    }

    // ------------------------------------------------------------------
    // Test 5: Service NotFound (run) → 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_service_returns_run_not_found()
    {
        var principal = MakePrincipal();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.NotFound("run-not-found", "Run not found."));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { }), "missing-run", ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#run-not-found", problem.Type);
    }

    // ------------------------------------------------------------------
    // Test 6: Service NotFound (raider) → 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_service_returns_raider_not_found()
    {
        var principal = MakePrincipal();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.NotFound("raider-not-found", "Raider not found."));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "x" }), "run-1", ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#raider-not-found", problem.Type);
        Assert.Equal("Raider not found.", problem.Detail);
    }

    // ------------------------------------------------------------------
    // Test 7: Service Forbidden — returns 403 + audit failure entry
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_and_emits_audit_when_service_returns_forbidden()
    {
        var principal = MakePrincipal(battleNetId: "bnet-other");
        var logger = new TestLogger<RunsUpdateFunction>();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Forbidden(
                "run-update-not-creator",
                "Only the run creator can update this run.",
                AuditReason: "not creator"));

        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Hacked" }), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        Assert.Single(
            logger.Entries,
            e => e.IsAudit("run.update", "failure", "not creator"));
    }

    // ------------------------------------------------------------------
    // Test 8: Service ConflictResult (editing closed) → 409
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_409_when_service_returns_conflict()
    {
        var principal = MakePrincipal();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.ConflictResult(
                "run-editing-closed",
                "Editing is closed for this run."));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Too late" }), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // ------------------------------------------------------------------
    // Test 9: Service PreconditionFailed (stale If-Match) → 412 + audit
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_412_and_emits_audit_when_service_returns_precondition_failed()
    {
        var principal = MakePrincipal();
        var logger = new TestLogger<RunsUpdateFunction>();

        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.PreconditionFailed(
                "if-match-stale",
                "The run was modified since you loaded it. Reload and try again."));

        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest(new { description = "Updated" }, ifMatch: "\"stale-etag\""),
            "run-1",
            ctx,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(412, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#if-match-stale", problem.Type);

        Assert.Single(logger.Entries, e => e.IsAudit("run.update", "failure", "if-match stale"));
    }

    // ------------------------------------------------------------------
    // Test 10: Missing If-Match header — returns 428 Precondition Required
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_428_when_if_match_header_is_missing()
    {
        var principal = MakePrincipal();
        var service = new Mock<IRunUpdateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest(new { description = "x" }, ifMatch: null),
            "run-1",
            ctx,
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(428, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#if-match-required", problem.Type);

        // Handler must short-circuit before calling the service.
        service.Verify(s => s.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<UpdateRunRequest>(),
            It.IsAny<RunUpdatePresentFields>(),
            It.IsAny<string>(),
            It.IsAny<SessionPrincipal>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 11: Malformed JSON returns 400 with a static error string,
    // never echoing the JsonException message (which can leak the
    // caller's payload offset/line/path).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_400_with_static_message_on_malformed_json()
    {
        var principal = MakePrincipal();
        var service = new Mock<IRunUpdateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{not valid json"));
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Headers["If-Match"] = DefaultTestEtag;

        var result = await fn.Run(httpContext.Request, "run-1", ctx, CancellationToken.None);

        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#invalid-body", problem.Type);
        Assert.Equal("Request body is invalid or missing.", problem.Detail);
    }

    // ------------------------------------------------------------------
    // Test 12: Function projects JsonDocument property presence into
    // RunUpdatePresentFields correctly — explicit nulls count as present.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_marks_present_fields_for_explicit_null_instance_id()
    {
        var principal = MakePrincipal();
        var updated = MakeUpdatedRun();

        RunUpdatePresentFields? capturedPresent = null;
        var service = new Mock<IRunUpdateService>();
        service.Setup(s => s.UpdateAsync(
                It.IsAny<string>(),
                It.IsAny<UpdateRunRequest>(),
                It.IsAny<RunUpdatePresentFields>(),
                It.IsAny<string>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, UpdateRunRequest, RunUpdatePresentFields, string, SessionPrincipal, CancellationToken>(
                (_, _, present, _, _, _) => capturedPresent = present)
            .ReturnsAsync(new RunOperationResult.Ok(updated));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            MakePutRequest("""
                {
                  "instanceId": null,
                  "difficulty": "MYTHIC_KEYSTONE",
                  "size": 5,
                  "keystoneLevel": 10
                }
                """),
            "run-1",
            ctx,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(capturedPresent);
        Assert.True(capturedPresent!.InstanceId);
        Assert.True(capturedPresent.Difficulty);
        Assert.True(capturedPresent.Size);
        Assert.True(capturedPresent.KeystoneLevel);
        Assert.False(capturedPresent.Description);
        Assert.False(capturedPresent.Visibility);
        Assert.False(capturedPresent.SignupCloseTime);
        Assert.False(capturedPresent.StartTime);
    }
}
