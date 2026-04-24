// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves <c>POST /api/admin/runs/migrate-schema</c> (site-admin only).
///
/// Backfills <c>Difficulty</c> / <c>Size</c> / <c>KeystoneLevel</c> on every
/// existing <see cref="RunDocument"/> by parsing the legacy composite
/// <c>ModeKey</c>. Idempotent: documents whose typed fields are already
/// populated are skipped. Supports a <c>?dryRun=true</c> query parameter
/// that reports the would-migrate count without writing anything.
///
/// Ships as part of PR 5 of the create-run-page-improvements plan; intended
/// to be invoked once post-deploy and then left alone. Not wired into the
/// admin UI yet — a curl call from an admin session is the expected path.
/// </summary>
public class RunsMigrateSchemaFunction(
    ISiteAdminService siteAdmin,
    IRunsRepository runs,
    ILogger<RunsMigrateSchemaFunction> logger)
{
    [Function("runs-migrate-schema")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/runs/migrate-schema")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal();

        if (!await siteAdmin.IsAdminAsync(principal.BattleNetId, ct))
            return Problem.Forbidden(req.HttpContext, "admin-only", "Site administrator access required.");

        var dryRun = string.Equals(
            req.Query["dryRun"].ToString(),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var result = await runs.MigrateSchemaAsync(dryRun, ct);

        AuditLog.Emit(logger, new AuditEvent(
            "runs.migrate-schema",
            principal.BattleNetId,
            null,
            "success",
            $"scanned={result.Scanned} migrated={result.Migrated} dryRun={result.DryRun}"));

        return new OkObjectResult(result);
    }

    /// <summary>
    /// <c>/api/v1/admin/runs/migrate-schema</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-migrate-schema-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/runs/migrate-schema")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, ctx, ct);
}
