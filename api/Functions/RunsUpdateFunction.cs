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
using Lfm.Api.Repositories;
using Lfm.Api.Runs;
using Lfm.Api.Services;
using Lfm.Api.Validation;
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

        // 0. Require an If-Match header carrying the ETag from the previous
        //    GET /api/runs/{id}. Optimistic concurrency guard — rejects the
        //    "two tabs, stale form" case with RFC 9110 428 Precondition
        //    Required before any work.
        if (!req.Headers.TryGetValue("If-Match", out var ifMatchValues)
            || string.IsNullOrWhiteSpace(ifMatchValues.ToString()))
        {
            return Problem.PreconditionRequired(
                req.HttpContext,
                "if-match-required",
                "This resource requires an If-Match header echoing the ETag from a prior GET.");
        }
        var ifMatchEtag = ifMatchValues.ToString();

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
        var isCreator = RunAccessPolicy.IsCreator(existing, principal.BattleNetId);
        if (!isCreator)
        {
            if (!RunAccessPolicy.IsGuildPeer(existing, principal.BattleNetId, guildId))
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

        // 3. Parse and validate request body. Keep the raw JsonElement long
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
        var hasStartTime = HasJsonProperty(root, "startTime");
        var hasSignupCloseTime = HasJsonProperty(root, "signupCloseTime");
        var hasDescription = HasJsonProperty(root, "description");
        var hasVisibility = HasJsonProperty(root, "visibility");
        var hasInstanceId = HasJsonProperty(root, "instanceId");
        var hasDifficulty = HasJsonProperty(root, "difficulty");
        var hasSize = HasJsonProperty(root, "size");
        var hasKeystoneLevel = HasJsonProperty(root, "keystoneLevel");

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
            if (hasInstanceId && body.InstanceId != existing.InstanceId)
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
        var effectiveStartTime = hasStartTime
            ? body.StartTime ?? existing.StartTime
            : existing.StartTime;
        var effectiveSignupCloseTime = hasSignupCloseTime
            ? body.SignupCloseTime ?? ""
            : existing.SignupCloseTime;
        var effectiveDescription = hasDescription
            ? body.Description ?? ""
            : existing.Description;
        var effectiveVisibility = hasVisibility
            ? body.Visibility ?? existing.Visibility
            : existing.Visibility;
        var effectiveInstanceId = hasInstanceId
            ? body.InstanceId
            : existing.InstanceId;
        var effectiveDifficulty = hasDifficulty
            ? body.Difficulty ?? existing.Difficulty
            : existing.Difficulty;
        var effectiveSize = hasSize
            ? body.Size ?? existing.Size
            : existing.Size;
        // ModeKey stays in storage only — derived here so legacy reads still
        // resolve. The wire no longer carries it.
        var effectiveModeKey = $"{effectiveDifficulty}:{effectiveSize}";
        var effectiveKeystoneLevel = hasKeystoneLevel
            ? body.KeystoneLevel
            : existing.KeystoneLevel;

        var shapeErrors = ValidateEffectiveRunShape(
            effectiveStartTime,
            effectiveSignupCloseTime,
            effectiveInstanceId,
            effectiveDifficulty,
            effectiveKeystoneLevel);
        if (shapeErrors.Count > 0)
            return Problem.BadRequest(
                req.HttpContext,
                "validation-failed",
                "Request body failed validation.",
                new Dictionary<string, object?> { ["errors"] = shapeErrors });

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
            StartTime = effectiveStartTime,
            SignupCloseTime = effectiveSignupCloseTime,
            Description = effectiveDescription,
            ModeKey = effectiveModeKey,
            Difficulty = effectiveDifficulty,
            Size = effectiveSize,
            KeystoneLevel = effectiveKeystoneLevel,
            Visibility = effectiveVisibility,
            InstanceId = effectiveInstanceId,
            InstanceName = effectiveInstanceName,
            CreatorGuild = isGuildPromotion
                ? (guildName ?? "")
                : existing.CreatorGuild,
            CreatorGuildId = isGuildPromotion
                ? (guildId is not null && int.TryParse(guildId, out var gid) ? gid : existing.CreatorGuildId)
                : existing.CreatorGuildId,
        };

        // 9. Replace in Cosmos. A stale If-Match surfaces as ConcurrencyConflict
        //    from the repo — map it to 412 Precondition Failed so the client can
        //    reload and retry.
        RunDocument persisted;
        try
        {
            persisted = await repo.UpdateAsync(updated, ifMatchEtag, ct);
        }
        catch (ConcurrencyConflictException)
        {
            AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "failure", "if-match stale"));
            return Problem.PreconditionFailed(
                req.HttpContext,
                "if-match-stale",
                "The run was modified since you loaded it. Reload and try again.");
        }

        AuditLog.Emit(logger, new AuditEvent("run.update", principal.BattleNetId, id, "success", null));

        // Echo the new ETag so a follow-up PUT without reloading still works.
        if (!string.IsNullOrEmpty(persisted.ETag))
            req.HttpContext.Response.Headers.ETag = persisted.ETag;

        return new OkObjectResult(RunResponseMapper.ToDetail(persisted, principal.BattleNetId));
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

    private static IReadOnlyList<string> ValidateEffectiveRunShape(
        string startTime,
        string signupCloseTime,
        int? instanceId,
        string difficulty,
        int? keystoneLevel)
    {
        var errors = new List<string>();
        if (!RunRequestTimeRules.SignupCloseTimeIsBeforeStartTime(signupCloseTime, startTime))
            errors.Add("signupCloseTime must be before startTime");

        if (instanceId is null && difficulty != CreateRunRequestValidator.MythicKeystone)
            errors.Add("instanceId is required for non-Mythic+ runs");

        if (difficulty != CreateRunRequestValidator.MythicKeystone && keystoneLevel is not null)
            errors.Add("keystoneLevel is only valid for Mythic+ runs");

        if (difficulty == CreateRunRequestValidator.MythicKeystone
            && instanceId is null
            && keystoneLevel is null)
            errors.Add("keystoneLevel is required when no specific dungeon is selected");

        return errors;
    }
}
