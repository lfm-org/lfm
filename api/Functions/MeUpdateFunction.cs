// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Validation;
using Lfm.Contracts.Me;

namespace Lfm.Api.Functions;

public class MeUpdateFunction(IRaidersRepository repo)
{
    private const int TtlDays = 180;
    private const int TtlSeconds = TtlDays * 86400;

    [Function("me-update")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var body = await JsonSerializer.DeserializeAsync<UpdateMeRequest>(
            req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);

        if (body is null)
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");

        var validator = new UpdateMeRequestValidator();
        var validationResult = await validator.ValidateAsync(body, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
            return Problem.BadRequest(
                req.HttpContext,
                "validation-failed",
                "Request body failed validation.",
                new Dictionary<string, object?> { ["errors"] = errors });
        }

        // Read-modify-write with optimistic concurrency. PATCH callers must
        // echo the ETag from the previous GET /api/v1/me as If-Match so stale
        // profile edits fail instead of overwriting newer state.
        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var updated = raider with { Locale = body.Locale!, Ttl = TtlSeconds };

        var ifMatchEtag = ResolveIfMatch(req);
        if (ifMatchEtag is null)
            return Problem.PreconditionRequired(
                req.HttpContext,
                "if-match-required",
                "This resource requires an If-Match header echoing the ETag from a prior GET.");

        RaiderDocument persisted;
        try
        {
            persisted = await repo.ReplaceAsync(updated, ifMatchEtag, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Problem.PreconditionFailed(
                req.HttpContext,
                "if-match-stale",
                "Your profile was modified since loaded. Reload and try again.");
        }

        if (!string.IsNullOrEmpty(persisted.ETag))
            req.HttpContext.Response.Headers.ETag = persisted.ETag;

        return new OkObjectResult(new UpdateMeResponse(Locale: body.Locale!));
    }

    /// <summary>
    /// <c>/api/v1/me</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("me-update-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);

    /// <summary>
    /// Returns the caller's concrete <c>If-Match</c> ETag when present.
    /// Missing, empty, and wildcard values fail the precondition contract.
    /// </summary>
    private static string? ResolveIfMatch(HttpRequest req)
    {
        if (!req.Headers.TryGetValue("If-Match", out var values))
            return null;
        var value = values.ToString();
        if (string.IsNullOrWhiteSpace(value) || value == "*")
            return null;
        return value;
    }
}
