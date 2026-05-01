// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Validation;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Implements the run-update policy: load the existing run, gate edit access
/// on creator-or-guild-peer, enforce editability + locked-field rules, resolve
/// effective field values from the (body, presentFields) pair, validate the
/// resulting run shape, look up the canonical instance name, and replace the
/// document in Cosmos with optimistic concurrency.
///
/// Returns a <see cref="RunOperationResult"/> that the Function adapter
/// translates to HTTP. Audit emission for the success and forbidden paths
/// stays at the Function — same pattern as <see cref="RunCreateService"/>.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-update.ts</c>.
/// </summary>
public sealed class RunUpdateService(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IGuildPermissions guildPermissions,
    IInstancesRepository instancesRepo) : IRunUpdateService
{
    public async Task<RunOperationResult> UpdateAsync(
        string runId,
        UpdateRunRequest body,
        RunUpdatePresentFields presentFields,
        string ifMatchEtag,
        SessionPrincipal principal,
        CancellationToken ct)
    {
        // 1. Load existing run.
        var existing = await runsRepo.GetByIdAsync(runId, ct);
        if (existing is null)
            return new RunOperationResult.NotFound("run-not-found", "Run not found.");

        // 2. Load the raider once and derive guild info from the selected character.
        //    principal.GuildId / GuildName are legacy session fields; guild info is
        //    always taken from the raider's stored selected character.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new RunOperationResult.NotFound("raider-not-found", "Raider not found.");

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        // 3. Permission check — mirrors runs-update.ts:
        //    Creator can always edit. Non-creator must be in the same guild with
        //    canCreateGuildRuns permission.
        var isCreator = RunAccessPolicy.IsCreator(existing, principal.BattleNetId);
        if (!isCreator)
        {
            if (!RunAccessPolicy.IsGuildPeer(existing, principal.BattleNetId, guildId))
            {
                return new RunOperationResult.Forbidden(
                    "run-update-not-creator",
                    "Only the run creator can update this run.",
                    AuditReason: "not creator");
            }

            var canEdit = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canEdit)
            {
                return new RunOperationResult.Forbidden(
                    "guild-rank-denied",
                    "Your guild rank does not have permission to edit guild runs.",
                    AuditReason: "guild rank denied");
            }
        }

        // 4. Editability check — mirrors isEditingClosed in run-editability.ts.
        if (RunEditability.IsEditingClosed(existing.SignupCloseTime, existing.StartTime, DateTimeOffset.UtcNow))
        {
            return new RunOperationResult.ConflictResult(
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
                return new RunOperationResult.BadRequest(
                    "start-time-locked",
                    "Cannot change start time after signups.");
            if (presentFields.InstanceId && body.InstanceId != existing.InstanceId)
                return new RunOperationResult.BadRequest(
                    "instance-locked",
                    "Cannot change instance after signups.");
        }

        if (presentFields.Visibility && body.Visibility == "PUBLIC")
        {
            return new RunOperationResult.BadRequest(
                "visibility-must-be-guild",
                "Visibility must be GUILD.");
        }

        // 6. Legacy visibility promotion guard.
        var isGuildPromotion = existing.Visibility != "GUILD";
        if (isGuildPromotion)
        {
            if (guildId is null)
                return new RunOperationResult.BadRequest(
                    "guild-required",
                    "A guild run requires an active character in a guild.");

            var canCreate = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canCreate)
            {
                return new RunOperationResult.Forbidden(
                    "guild-rank-denied",
                    "Guild run creation is not enabled for your rank.",
                    AuditReason: "guild rank denied");
            }
        }

        // 7. Resolve effective instanceId + mode fields and look up the
        //    instance name.
        var effectiveStartTime = presentFields.StartTime
            ? body.StartTime ?? existing.StartTime
            : existing.StartTime;
        var effectiveSignupCloseTime = presentFields.SignupCloseTime
            ? body.SignupCloseTime ?? ""
            : existing.SignupCloseTime;
        var effectiveDescription = presentFields.Description
            ? body.Description ?? ""
            : existing.Description;
        var effectiveVisibility = "GUILD";
        var effectiveInstanceId = presentFields.InstanceId
            ? body.InstanceId
            : existing.InstanceId;
        var effectiveDifficulty = presentFields.Difficulty
            ? body.Difficulty ?? existing.Difficulty
            : existing.Difficulty;
        var effectiveSize = presentFields.Size
            ? body.Size ?? existing.Size
            : existing.Size;
        // ModeKey stays in storage only — derived here so legacy reads still
        // resolve. The wire no longer carries it.
        var effectiveModeKey = $"{effectiveDifficulty}:{effectiveSize}";
        var effectiveKeystoneLevel = presentFields.KeystoneLevel
            ? body.KeystoneLevel
            : existing.KeystoneLevel;

        var shapeErrors = ValidateEffectiveRunShape(
            effectiveStartTime,
            effectiveSignupCloseTime,
            effectiveInstanceId,
            effectiveDifficulty,
            effectiveKeystoneLevel);
        if (shapeErrors.Count > 0)
            return new RunOperationResult.BadRequest(
                "validation-failed",
                "Request body failed validation.",
                Errors: shapeErrors);

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
                return new RunOperationResult.ServiceUnavailable(
                    "instance-data-unavailable",
                    "Instance data not available.");

            var matchedInstance = instances.FirstOrDefault(i =>
                i.InstanceNumericId == effectiveInstanceId.Value
                && i.Difficulty == effectiveDifficulty
                && i.Size == effectiveSize);
            if (matchedInstance is null)
                return new RunOperationResult.BadRequest(
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
        try
        {
            var persisted = await runsRepo.UpdateAsync(updated, ifMatchEtag, ct);
            return new RunOperationResult.Ok(persisted);
        }
        catch (ConcurrencyConflictException)
        {
            return new RunOperationResult.PreconditionFailed(
                "if-match-stale",
                "The run was modified since you loaded it. Reload and try again.");
        }
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
