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
/// Serves PUT /api/runs/{id}.
///
/// HTTP adapter for <see cref="IRunUpdateService"/>. The handler validates the
/// If-Match header (RFC 9110 428 short-circuit), parses the body twice — once
/// into <see cref="UpdateRunRequest"/> for FluentValidation, once into a
/// <see cref="JsonDocument"/> to distinguish omitted fields from explicit nulls
/// (the <see cref="RunUpdatePresentFields"/> projection) — calls the service,
/// then translates the resulting <see cref="RunOperationResult"/>. Audit
/// emission for the success, forbidden, and stale-If-Match paths stays at this
/// layer because each event ties to the HTTP-shaped boundary (status code,
/// idempotency, traceparent).
///
/// Run-update policy itself (load existing, edit-access gate, editability +
/// locked-field rules, effective-shape resolution, repo replace) lives in
/// <see cref="RunUpdateService"/>.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-update.ts</c>.
/// </summary>
public class RunsUpdateFunction(IRunUpdateService service, ILogger<RunsUpdateFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("runs-update")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // Require an If-Match header carrying the ETag from the previous
        // GET /api/runs/{id}. Optimistic concurrency guard — rejects the
        // "two tabs, stale form" case with RFC 9110 428 Precondition
        // Required before any work.
        if (!req.Headers.TryGetValue("If-Match", out var ifMatchValues)
            || string.IsNullOrWhiteSpace(ifMatchValues.ToString()))
        {
            return Problem.PreconditionRequired(
                req.HttpContext,
                "if-match-required",
                "This resource requires an If-Match header echoing the ETag from a prior GET.");
        }
        var ifMatchEtag = ifMatchValues.ToString();

        // Parse and validate request body. Keep the raw JsonElement long
        // enough to distinguish omitted fields from explicit nulls.
        UpdateRunRequest? body;
        JsonDocument bodyDoc;
        try
        {
            bodyDoc = await JsonDocument.ParseAsync(
                req.Body,
                cancellationToken: ct);

            if (bodyDoc.RootElement.ValueKind != JsonValueKind.Object)
                return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");

            body = bodyDoc.RootElement.Deserialize<UpdateRunRequest>(JsonOptions);
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

        using var parsedBody = bodyDoc;
        var validator = new UpdateRunRequestValidator();
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

        var root = parsedBody.RootElement;
        var presentFields = new RunUpdatePresentFields(
            StartTime: HasJsonProperty(root, "startTime"),
            SignupCloseTime: HasJsonProperty(root, "signupCloseTime"),
            Description: HasJsonProperty(root, "description"),
            Visibility: HasJsonProperty(root, "visibility"),
            InstanceId: HasJsonProperty(root, "instanceId"),
            Difficulty: HasJsonProperty(root, "difficulty"),
            Size: HasJsonProperty(root, "size"),
            KeystoneLevel: HasJsonProperty(root, "keystoneLevel"));

        var result = await service.UpdateAsync(id, body, presentFields, ifMatchEtag, principal, ct);

        // Audit-bearing branches stay at the function (this layer owns audit shape);
        // pure HTTP-shape branches go through the shared translator.
        switch (result)
        {
            case RunOperationResult.Ok ok:
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "success", null));
                // Echo the new ETag so a follow-up PUT without reloading still works.
                if (!string.IsNullOrEmpty(ok.Run.ETag))
                    req.HttpContext.Response.Headers.ETag = ok.Run.ETag;
                return new OkObjectResult(RunResponseMapper.ToDetail(ok.Run, principal.BattleNetId));
            case RunOperationResult.Forbidden fb:
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", fb.AuditReason));
                return result.ToProblemResult(req.HttpContext);
            case RunOperationResult.PreconditionFailed pf:
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", pf.Code.Replace('-', ' ')));
                return result.ToProblemResult(req.HttpContext);
            default:
                return result.ToProblemResult(req.HttpContext);
        }
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}</c> PUT alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-update-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);

    private static bool HasJsonProperty(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals(name)
                || string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
