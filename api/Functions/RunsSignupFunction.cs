// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Runs;
using Lfm.Api.Validation;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves POST /api/runs/{id}/signup.
///
/// HTTP adapter for <see cref="IRunSignupService"/>. The handler deserializes
/// the request body, runs DTO validation, calls the service, and translates
/// the resulting <see cref="RunOperationResult"/> to a 200 OK response or the
/// appropriate <c>problem+json</c> error. Audit emission for the success and
/// forbidden paths stays at this layer because both events tie to the
/// HTTP-shaped boundary (status code, idempotency, traceparent).
///
/// Run-signup policy itself (raider lookup, character ownership, run load,
/// editability + GUILD-permission gates, rejection-list IN→OUT flip,
/// concurrency retry loop) lives in <see cref="RunSignupService"/>.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-signup.ts</c>.
/// </summary>
public class RunsSignupFunction(IRunSignupService service, ILogger<RunsSignupFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("runs-signup")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs/{id}/signup")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // Parse and validate request body (request-scoped, not retry-scoped).
        SignupRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SignupRequest>(
                req.Body,
                JsonOptions,
                cancellationToken: ct);

            if (body is null)
                return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }
        catch (JsonException)
        {
            // Never echo JsonException.Message — it can disclose offset/line/path
            // detail from the caller's payload that is not useful to the user and
            // inconsistent with how other handlers report parse failures.
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }

        var validator = new SignupRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
            return Problem.BadRequest(
                req.HttpContext,
                "validation-failed",
                "Request body failed validation.",
                new Dictionary<string, object?> { ["errors"] = errors });
        }

        var result = await service.SignupAsync(id, body, principal, ct);

        // Audit-bearing branches stay at the function (this layer owns audit shape);
        // pure HTTP-shape branches go through the shared translator.
        switch (result)
        {
            case RunOperationResult.Ok ok:
                AuditLog.Emit(logger, new AuditEvent("signup.create", principal.BattleNetId, id, "success", null));
                return new OkObjectResult(RunResponseMapper.ToDetail(ok.Run, principal.BattleNetId));
            case RunOperationResult.Forbidden fb:
                AuditLog.Emit(logger, new AuditEvent("signup.create", principal.BattleNetId, id, "failure", fb.AuditReason));
                return result.ToProblemResult(req.HttpContext);
            default:
                return result.ToProblemResult(req.HttpContext);
        }
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}/signup</c> POST alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-signup-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/runs/{id}/signup")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);
}
