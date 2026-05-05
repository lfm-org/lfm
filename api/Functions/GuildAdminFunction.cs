// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/guild/admin?guildId={id}.
///
/// Returns the guild document for any guild by ID.
/// Restricted to site administrators.
/// </summary>
public class GuildAdminFunction(
    IGuildRepository guildRepo,
    ISiteAdminService siteAdmin,
    ILogger<GuildAdminFunction>? logger = null)
{
    private ILogger<GuildAdminFunction> Logger => logger ?? NullLogger<GuildAdminFunction>.Instance;

    [Function("guild-admin-get")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "patch", Route = "guild/admin")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee
        var guildId = req.Query["guildId"].FirstOrDefault();

        if (!await siteAdmin.IsAdminAsync(principal.BattleNetId, cancellationToken))
        {
            if (HttpMethods.IsPatch(req.Method))
            {
                AuditLog.Emit(Logger, new AuditEvent(
                    "guild.admin.update",
                    principal.BattleNetId,
                    string.IsNullOrWhiteSpace(guildId) ? null : guildId,
                    "failure",
                    "forbidden"));
            }
            return Problem.Forbidden(req.HttpContext, "admin-only", "Site administrator access required.");
        }

        if (string.IsNullOrWhiteSpace(guildId))
        {
            if (HttpMethods.IsPatch(req.Method))
                AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", principal.BattleNetId, null, "failure", "missing guildId"));
            return Problem.BadRequest(req.HttpContext, "missing-parameter", "guildId query parameter is required.");
        }

        var guildDoc = await guildRepo.GetAsync(guildId, cancellationToken);
        if (guildDoc is null)
        {
            if (HttpMethods.IsPatch(req.Method))
                AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", principal.BattleNetId, guildId, "failure", "not found"));
            return Problem.NotFound(req.HttpContext, "guild-not-found", "Guild not found.");
        }

        if (HttpMethods.IsPatch(req.Method))
            return await UpdateAsync(req, principal.BattleNetId, guildId, guildDoc, cancellationToken);

        GuildSettingsUpdate.EmitEtag(req, guildDoc);
        return new OkObjectResult(GuildMapper.MapToDto(guildDoc, GuildEffectivePermissions.SiteAdminView));
    }

    /// <summary>
    /// <c>/api/v1/guild/admin</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("guild-admin-get-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "patch", Route = "v1/guild/admin")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);

    private async Task<IActionResult> UpdateAsync(
        HttpRequest req,
        string actorBattleNetId,
        string guildId,
        GuildDocument guildDoc,
        CancellationToken cancellationToken)
    {
        var (body, validationError) = await GuildSettingsUpdate.ReadValidatedRequestAsync(req, cancellationToken);
        if (validationError is not null)
        {
            AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", actorBattleNetId, guildId, "failure", "invalid input"));
            return validationError;
        }

        var ifMatchEtag = GuildSettingsUpdate.ResolveIfMatch(req);
        if (ifMatchEtag is null)
        {
            AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", actorBattleNetId, guildId, "failure", "if-match required"));
            return Problem.PreconditionRequired(
                req.HttpContext,
                "if-match-required",
                "This resource requires an If-Match header echoing the ETag from a prior GET.");
        }

        GuildDocument persisted;
        try
        {
            persisted = await guildRepo.ReplaceAsync(GuildSettingsUpdate.Apply(guildDoc, body!), ifMatchEtag, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", actorBattleNetId, guildId, "failure", "if-match stale"));
            return Problem.PreconditionFailed(
                req.HttpContext,
                "if-match-stale",
                "Guild settings were modified since loaded. Reload and try again.");
        }

        AuditLog.Emit(Logger, new AuditEvent("guild.admin.update", actorBattleNetId, guildId, "success", null));
        GuildSettingsUpdate.EmitEtag(req, persisted);
        return new OkObjectResult(GuildMapper.MapToDto(persisted, GuildEffectivePermissions.SiteAdminView));
    }
}
