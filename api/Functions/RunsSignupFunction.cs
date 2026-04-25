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
using Lfm.Contracts.WoW;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves POST /api/runs/{id}/signup.
///
/// Lets an authenticated user sign up (or update their signup) for a run with a
/// specific character. The signup is stored as a <see cref="RunCharacterEntry"/>
/// embedded in the run document's <c>runCharacters</c> array.
///
/// Logic:
///   1. Load the run — 404 if not found.
///   2. For GUILD runs: check visibility access and <c>canSignupGuildRuns</c> rank permission.
///   3. Validate the request body (characterId, desiredAttendance, specId).
///   4. Verify the caller owns the character (must be in their raider doc's characters list).
///   5. Upsert the <see cref="RunCharacterEntry"/> — one signup per battleNetId per run.
///   6. Persist the run document.
///   7. Return the sanitized run.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-signup.ts</c>.
/// </summary>
public class RunsSignupFunction(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IGuildPermissions guildPermissions,
    ILogger<RunsSignupFunction> logger)
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

        // 1. Parse and validate request body (request-scoped, not retry-scoped).
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

        // 2. Load raider document and verify character ownership.
        //    Mirrors: const storedCharacter = raider.characters.find(c => c.id === body.characterId)
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        // Derive the caller's guild from the raider's selected character for the
        // GUILD visibility check below. principal.GuildId is a legacy session field
        // and is no longer populated.
        var (callerGuildId, _) = GuildResolver.FromRaider(raider);

        var storedCharacter = raider.Characters?.FirstOrDefault(c => c.Id == body.CharacterId);
        if (storedCharacter is null)
            return Problem.BadRequest(
                req.HttpContext,
                "character-not-on-profile",
                "Character not found on your profile.");

        // 3. Resolve spec info — mirrors the specId block in runs-signup.ts.
        int? specId = body.SpecId;
        string? specName = null;
        string? role = null;

        if (specId is not null)
        {
            var specEntry = storedCharacter.SpecializationsSummary?.Specializations
                ?.FirstOrDefault(s => s.Specialization.Id == specId.Value);
            if (specEntry is null)
                return Problem.BadRequest(
                    req.HttpContext,
                    "invalid-spec-id",
                    "Invalid specId: not found on character.");

            specName = specEntry.Specialization.Name;
        }

        // 4. Read-modify-write loop with optimistic concurrency.
        //    On ConcurrencyConflictException the loop re-reads the run and retries.
        const int maxAttempts = 3;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 4a. Load existing run.
            var run = await runsRepo.GetByIdAsync(id, ct);
            if (run is null)
                return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

            // 4b. Signup close time check — reject if signups are closed.
            if (RunEditability.IsEditingClosed(run.SignupCloseTime, run.StartTime, DateTimeOffset.UtcNow))
            {
                return Problem.Conflict(
                    req.HttpContext,
                    "signups-closed",
                    "Signups are closed for this run.");
            }

            // 4c. Visibility check for GUILD runs — mirrors runs-signup.ts.
            if (run.Visibility == "GUILD")
            {
                var isCreator = run.CreatorBattleNetId == principal.BattleNetId;
                var isGuildMember = callerGuildId is not null
                    && run.CreatorGuildId is not null
                    && run.CreatorGuildId.ToString() == callerGuildId;

                if (!isCreator && !isGuildMember)
                    return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

                var canSignup = await guildPermissions.CanSignupGuildRunsAsync(raider, ct);
                if (!canSignup)
                {
                    AuditLog.Emit(logger, new AuditEvent("signup.create", principal.BattleNetId, id, "failure", "guild rank denied"));
                    return Problem.Forbidden(
                        req.HttpContext,
                        "guild-rank-denied",
                        "Guild signup is not enabled for your rank.");
                }
            }

            // 4d. Upsert the RunCharacterEntry — one per battleNetId per run.
            var existingIndex = -1;
            for (var i = 0; i < run.RunCharacters.Count; i++)
            {
                if (run.RunCharacters[i].RaiderBattleNetId == principal.BattleNetId)
                {
                    existingIndex = i;
                    break;
                }
            }

            var entryId = existingIndex >= 0
                ? run.RunCharacters[existingIndex].Id
                : Guid.NewGuid().ToString();

            // ReviewedAttendance defaults to "IN" for a brand-new signup. For an
            // edit (existing entry present), the prior value is preserved so the
            // run owner's review decision survives a self-edit. For a re-signup
            // *after* a previous rejection (entry was removed by cancel, but the
            // raider sits in the run's rejection list), the default flips to
            // "OUT" — closes the cancel-then-resignup bypass documented in
            // docs/threat-models/run-signup-peer-permission.md.
            var rejected = run.RejectedRaiderBattleNetIds ?? [];
            var reviewedAttendance = existingIndex >= 0
                ? run.RunCharacters[existingIndex].ReviewedAttendance
                : rejected.Contains(principal.BattleNetId, StringComparer.Ordinal)
                    ? "OUT"
                    : "IN";

            var entry = new RunCharacterEntry(
                Id: entryId,
                CharacterId: storedCharacter.Id,
                CharacterName: storedCharacter.Name,
                CharacterRealm: storedCharacter.Realm,
                CharacterLevel: storedCharacter.Level ?? 0,
                CharacterClassId: storedCharacter.ClassId ?? 0,
                CharacterClassName: storedCharacter.ClassName
                    ?? (storedCharacter.ClassId is int cid ? WowClasses.GetName(cid) : ""),
                CharacterRaceId: 0,
                CharacterRaceName: "",
                RaiderBattleNetId: principal.BattleNetId,
                DesiredAttendance: body.DesiredAttendance!,
                ReviewedAttendance: reviewedAttendance,
                SpecId: specId,
                SpecName: specName,
                Role: role);

            var updatedCharacters = run.RunCharacters.ToList();
            if (existingIndex >= 0)
                updatedCharacters[existingIndex] = entry;
            else
                updatedCharacters.Add(entry);

            // 4e. Persist the updated run document with ETag check.
            var updated = run with { RunCharacters = updatedCharacters };
            try
            {
                // Pass null so the repository falls back to run.ETag — this is an
                // internal retry loop, not a client-driven If-Match flow.
                var persisted = await runsRepo.UpdateAsync(updated, ifMatchEtag: null, ct);

                AuditLog.Emit(logger, new AuditEvent("signup.create", principal.BattleNetId, id, "success", null));

                return new OkObjectResult(Sanitize(persisted, principal.BattleNetId));
            }
            catch (ConcurrencyConflictException)
            {
                logger.LogWarning("Concurrency conflict on signup for run {RunId}, attempt {Attempt}", id, attempt + 1);
                // Loop will re-read the document and retry.
            }
        }

        // All retry attempts exhausted.
        return Problem.Conflict(
            req.HttpContext,
            "concurrent-modification",
            "Concurrent modification, please retry.");
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

    // ------------------------------------------------------------------
    // Sanitizer — mirrors sanitizeRunDocumentForResponse in
    // functions/src/lib/runResponseSanitizer.ts
    // ------------------------------------------------------------------

    private static RunDetailDto Sanitize(RunDocument run, string currentBattleNetId)
    {
        var (difficulty, size) = Helpers.RunModeResolver.Resolve(run.Difficulty, run.Size, run.ModeKey);
        return new RunDetailDto(
            Id: run.Id,
            StartTime: run.StartTime,
            SignupCloseTime: run.SignupCloseTime,
            Description: run.Description,
            Visibility: run.Visibility,
            CreatorGuild: run.CreatorGuild,
            InstanceId: run.InstanceId,
            InstanceName: run.InstanceName,
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: run.KeystoneLevel,
            RunCharacters: run.RunCharacters
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
