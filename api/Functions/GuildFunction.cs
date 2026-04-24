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
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Guild;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/guild and PATCH /api/guild.
///
/// GET  — returns the guild home view for the current user's guild.
///         Returns <c>NoGuildDto</c> (200) when the raider exists but their
///         selected character has no guild — this is the graceful no-guild path.
///         Returns 404 when the raider document itself is missing, or the
///         derived guild document does not exist in Cosmos.
///         Mirrors the TypeScript <c>currentGuildHandler</c> in
///         <c>functions/src/functions/guild.ts</c>.
///
/// PATCH — updates guild settings (timezone, locale, slogan, rankPermissions).
///         Requires the caller to be a guild admin (rank 0 in the Blizzard roster).
///         Returns 403 when the caller is not an admin.
///         Returns 404 when the raider or guild document is missing.
///         Mirrors the TypeScript <c>saveCurrentGuildSettings</c> logic in
///         <c>functions/src/lib/guild/service.ts</c>.
/// </summary>
public class GuildFunction(IGuildRepository guildRepo, IRaidersRepository raidersRepo, IGuildPermissions guildPermissions, ILogger<GuildFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // ------------------------------------------------------------------
    // GET /api/guild
    // ------------------------------------------------------------------

    [Function("guild-get")]
    [RequireAuth]
    public async Task<IActionResult> GuildGet(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "guild")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // Derive the caller's guild from the raider's selected character. The
        // session principal's GuildId is a legacy field that is no longer populated.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var (guildId, _) = GuildResolver.FromRaider(raider);

        if (guildId is null)
            return new OkObjectResult(GuildMapper.NoGuildDto());

        var guildDoc = await guildRepo.GetAsync(guildId, cancellationToken);
        if (guildDoc is null)
            return Problem.NotFound(req.HttpContext, "guild-not-found", "Guild not found.");

        return new OkObjectResult(GuildMapper.MapToDto(guildDoc));
    }

    // ------------------------------------------------------------------
    // PATCH /api/guild
    // ------------------------------------------------------------------

    [Function("guild-update")]
    [RequireAuth]
    public async Task<IActionResult> GuildUpdate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "guild")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null)
            return Problem.NotFound(req.HttpContext, "guild-not-found", "Guild not found.");

        var isAdmin = await guildPermissions.IsAdminAsync(raider, cancellationToken);
        if (!isAdmin)
        {
            AuditLog.Emit(logger, new AuditEvent("guild.update", principal.BattleNetId, guildId, "failure", "forbidden"));
            return Problem.Forbidden(req.HttpContext, "guild-admin-only", "Guild admin access required.");
        }

        var body = await JsonSerializer.DeserializeAsync<UpdateGuildRequest>(
            req.Body,
            JsonOptions,
            cancellationToken: cancellationToken);

        if (body is null)
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");

        var validator = new UpdateGuildRequestValidator();
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

        var guildDoc = await guildRepo.GetAsync(guildId, cancellationToken);
        if (guildDoc is null)
            return Problem.NotFound(req.HttpContext, "guild-not-found", "Guild not found.");

        var updatedSetup = guildDoc.Setup is not null
            ? guildDoc.Setup with
            {
                InitializedAt = guildDoc.Setup.InitializedAt ?? DateTimeOffset.UtcNow.ToString("o"),
                Timezone = body.Timezone!,
                Locale = body.Locale!,
            }
            : new GuildSetup(
                InitializedAt: DateTimeOffset.UtcNow.ToString("o"),
                Timezone: body.Timezone!,
                Locale: body.Locale!);

        var updatedRankPermissions = body.RankPermissions is not null
            ? body.RankPermissions
                .Select(rp => new GuildRankPermission(rp.Rank, rp.CanCreateGuildRuns, rp.CanSignupGuildRuns, rp.CanDeleteGuildRuns))
                .ToList()
            : guildDoc.RankPermissions;

        var updatedDoc = guildDoc with
        {
            Slogan = body.Slogan ?? guildDoc.Slogan,
            Setup = updatedSetup,
            RankPermissions = updatedRankPermissions,
        };

        await guildRepo.UpsertAsync(updatedDoc, cancellationToken);

        AuditLog.Emit(logger, new AuditEvent("guild.update", principal.BattleNetId, guildId, "success", null));

        return new OkObjectResult(GuildMapper.MapToDto(updatedDoc));
    }
}
