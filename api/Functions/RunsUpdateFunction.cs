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
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves PUT /api/runs/{id}.
///
/// Updates an existing run (title, time, instance, visibility, etc.) using a
/// read-modify-write pattern: load → validate → apply changes → replace in Cosmos.
///
/// Permission rules (mirrors runs-update.ts):
///   - The creator can always edit their own run.
///   - A non-creator can edit a GUILD run if they belong to the same guild and
///     hold the <c>canCreateGuildRuns</c> rank permission.
///   - All other callers receive 403.
///
/// Editability rules (mirrors run-editability.ts):
///   - Editing is closed once signupCloseTime or startTime has passed → 409 Conflict.
///   - startTime and instanceId are locked once the run has at least one signup → 400.
///
/// GUILD visibility promotion (PUBLIC → GUILD):
///   - Requires the caller to belong to a guild and hold <c>canCreateGuildRuns</c>.
///   - Stamps creatorGuild / creatorGuildId from the caller's session.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-update.ts</c>.
/// </summary>
public class RunsUpdateFunction(IRunsRepository repo, IRaidersRepository raidersRepo, IGuildPermissions guildPermissions, IInstancesRepository instancesRepo, ILogger<RunsUpdateFunction> logger)
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

        // 1. Load existing run.
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null)
            return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

        // 2. Load the raider once and derive guild info from the selected character.
        //    principal.GuildId / GuildName are legacy session fields; guild info is
        //    always taken from the raider's stored selected character.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        // 3. Permission check — mirrors runs-update.ts:
        //    Creator can always edit. Non-creator must be in the same guild with
        //    canCreateGuildRuns permission.
        var isCreator = existing.CreatorBattleNetId == principal.BattleNetId;
        if (!isCreator)
        {
            if (existing.Visibility != "GUILD"
                || existing.CreatorGuildId is null
                || guildId != existing.CreatorGuildId.ToString())
            {
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", "not creator"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "run-update-not-creator",
                    "Only the run creator can update this run.");
            }

            var canEdit = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canEdit)
            {
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", "guild rank denied"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "guild-rank-denied",
                    "Your guild rank does not have permission to edit guild runs.");
            }
        }

        // 3. Parse and validate request body.
        UpdateRunRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateRunRequest>(
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

        // 4. Editability check — mirrors isEditingClosed in run-editability.ts.
        //    Returns 409 Conflict (the resource state conflicts with the request).
        if (RunEditability.IsEditingClosed(existing.SignupCloseTime, existing.StartTime, DateTimeOffset.UtcNow))
        {
            return Problem.Conflict(
                req.HttpContext,
                "run-editing-closed",
                "Editing is closed for this run.");
        }

        // 5. Locked-field check — mirrors getLockedFields in run-editability.ts.
        //    instanceId and startTime are locked once there is at least one signup.
        //    Only reject if the value actually changes (the form always sends all fields).
        var signupCount = existing.RunCharacters.Count;
        if (signupCount > 0)
        {
            if (body.StartTime is not null && body.StartTime != existing.StartTime)
                return Problem.BadRequest(
                    req.HttpContext,
                    "start-time-locked",
                    "Cannot change start time after signups.");
            if (body.InstanceId is not null && body.InstanceId != existing.InstanceId)
                return Problem.BadRequest(
                    req.HttpContext,
                    "instance-locked",
                    "Cannot change instance after signups.");
        }

        // 6. GUILD visibility promotion guard — mirrors isGuildVisibilityPromotion.
        var isGuildPromotion = body.Visibility == "GUILD" && existing.Visibility != "GUILD";
        if (isGuildPromotion)
        {
            if (guildId is null)
                return Problem.BadRequest(
                    req.HttpContext,
                    "guild-required",
                    "A guild run requires an active character in a guild.");

            var canCreate = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canCreate)
            {
                AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", "guild rank denied"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "guild-rank-denied",
                    "Guild run creation is not enabled for your rank.");
            }
        }

        // 7. Resolve effective instanceId + mode fields and look up the
        //    instance name.
        var effectiveInstanceId = body.InstanceId ?? existing.InstanceId;
        var effectiveDifficulty = body.Difficulty ?? existing.Difficulty;
        var effectiveSize = body.Size ?? existing.Size;
        // ModeKey stays in storage only — derived here so legacy reads still
        // resolve. The wire no longer carries it.
        var effectiveModeKey = $"{effectiveDifficulty}:{effectiveSize}";
        var effectiveKeystoneLevel = body.KeystoneLevel ?? existing.KeystoneLevel;

        // Load instances to validate the (instanceId, difficulty, size)
        // combination and obtain the canonical instance name. Each InstanceDto
        // row represents one (instance, mode) pair.
        //
        // A dungeon-agnostic Mythic+ run (effectiveInstanceId is null) skips
        // this validation — there is no specific instance to match.
        string? effectiveInstanceName = existing.InstanceName;
        if (effectiveInstanceId.HasValue)
        {
            var instances = await instancesRepo.ListAsync(ct);
            if (instances.Count == 0)
                return Problem.ServiceUnavailable(
                    req.HttpContext,
                    "instance-data-unavailable",
                    "Instance data not available.");

            var matchedInstance = instances.FirstOrDefault(i =>
                i.InstanceNumericId == effectiveInstanceId.Value
                && i.Difficulty == effectiveDifficulty
                && i.Size == effectiveSize);
            if (matchedInstance is null)
                return Problem.BadRequest(
                    req.HttpContext,
                    "invalid-instance-mode",
                    "Invalid difficulty/size for instance.");
            effectiveInstanceName = matchedInstance.Name;
        }
        else
        {
            effectiveInstanceName = null;
        }

        // 8. Apply changes — mirrors applyRunUpdate in runs-update.ts.
        var updated = existing with
        {
            StartTime = body.StartTime ?? existing.StartTime,
            SignupCloseTime = body.SignupCloseTime ?? existing.SignupCloseTime,
            Description = body.Description ?? existing.Description,
            ModeKey = effectiveModeKey,
            Difficulty = effectiveDifficulty,
            Size = effectiveSize,
            KeystoneLevel = effectiveKeystoneLevel,
            Visibility = body.Visibility ?? existing.Visibility,
            InstanceId = effectiveInstanceId,
            InstanceName = effectiveInstanceName,
            CreatorGuild = isGuildPromotion
                ? (guildName ?? "")
                : existing.CreatorGuild,
            CreatorGuildId = isGuildPromotion
                ? (guildId is not null && int.TryParse(guildId, out var gid) ? gid : existing.CreatorGuildId)
                : existing.CreatorGuildId,
        };

        // 9. Replace in Cosmos.
        var persisted = await repo.UpdateAsync(updated, ct);

        AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "success", null));

        return new OkObjectResult(MapToDto(persisted, principal.BattleNetId));
    }

    // ------------------------------------------------------------------
    // Mapping helper — projects the stored RunDocument to its wire DTO.
    // ------------------------------------------------------------------

    private static RunDetailDto MapToDto(RunDocument doc, string currentBattleNetId)
    {
        var (difficulty, size) = RunModeResolver.Resolve(doc.Difficulty, doc.Size, doc.ModeKey);
        return new RunDetailDto(
            Id: doc.Id,
            StartTime: doc.StartTime,
            SignupCloseTime: doc.SignupCloseTime,
            Description: doc.Description,
            Visibility: doc.Visibility,
            CreatorGuild: doc.CreatorGuild,
            InstanceId: doc.InstanceId,
            InstanceName: doc.InstanceName,
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: doc.KeystoneLevel,
            RunCharacters: doc.RunCharacters
                .Select(c => new RunCharacterDto(
                    CharacterName: c.CharacterName,
                    CharacterRealm: c.CharacterRealm,
                    CharacterClassId: c.CharacterClassId,
                    CharacterClassName: c.CharacterClassName,
                    DesiredAttendance: c.DesiredAttendance,
                    ReviewedAttendance: c.ReviewedAttendance,
                    SpecName: c.SpecName,
                    Role: c.Role,
                    IsCurrentUser: c.RaiderBattleNetId == currentBattleNetId))
                .ToList());
    }
}
