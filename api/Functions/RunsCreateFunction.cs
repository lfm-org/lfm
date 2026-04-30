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
/// Serves POST /api/runs.
///
/// HTTP adapter for <see cref="IRunCreateService"/>. The handler deserializes
/// the request body, runs DTO validation, calls the service, and translates
/// the resulting <see cref="RunOperationResult"/> to a 201 Created response
/// or the appropriate <c>problem+json</c> error. Audit emission for the
/// success and forbidden paths stays at this layer because both events tie
/// to the HTTP-shaped boundary (status code, idempotency, traceparent).
///
/// Run-create policy itself (raider lookup, guild guard, document
/// construction) lives in <see cref="RunCreateService"/>.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-create.ts</c>.
/// </summary>
public class RunsCreateFunction(IRunCreateService service, ILogger<RunsCreateFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("runs-create")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        CreateRunRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateRunRequest>(
                req.Body,
                JsonOptions,
                cancellationToken: ct);

            if (body is null)
                return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }
        catch (JsonException)
        {
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }

        var validator = new CreateRunRequestValidator();
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

        var result = await service.CreateAsync(body, principal, ct);

        // Audit-bearing branches stay at the function (this layer owns audit shape);
        // pure HTTP-shape branches go through the shared translator.
        switch (result)
        {
            case RunOperationResult.Ok ok:
                AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, ok.Run.Id, "success", null));
                return new ObjectResult(RunResponseMapper.ToDetail(ok.Run, principal.BattleNetId)) { StatusCode = 201 };
            case RunOperationResult.Forbidden fb:
                AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, null, "failure", fb.AuditReason));
                return result.ToProblemResult(req.HttpContext);
            default:
                return result.ToProblemResult(req.HttpContext);
        }
    }

    /// <summary>
    /// <c>/api/v1/runs</c> POST alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-create-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/runs")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, ctx, ct);
}
