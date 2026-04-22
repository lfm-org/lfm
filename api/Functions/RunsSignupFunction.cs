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
                return new BadRequestObjectResult(new { error = "Invalid request body" });
        }
        catch (JsonException)
        {
            // Never echo JsonException.Message — it can disclose offset/line/path
            // detail from the caller's payload that is not useful to the user and
            // inconsistent with how other handlers report parse failures.
            return new BadRequestObjectResult(new { error = "Invalid request body" });
        }

        var validator = new SignupRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(
                new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // 2. Load raider document and verify character ownership.
        //    Mirrors: const storedCharacter = raider.characters.find(c => c.id === body.characterId)
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new NotFoundObjectResult(new { error = "Raider not found" });

        // Derive the caller's guild from the raider's selected character for the
        // GUILD visibility check below. principal.GuildId is a legacy session field
        // and is no longer populated.
        var (callerGuildId, _) = GuildResolver.FromRaider(raider);

        var storedCharacter = raider.Characters?.FirstOrDefault(c => c.Id == body.CharacterId);
        if (storedCharacter is null)
            return new BadRequestObjectResult(new { error = "Character not found on your profile" });

        // 3. Resolve spec info — mirrors the specId block in runs-signup.ts.
        int? specId = body.SpecId;
        string? specName = null;
        string? role = null;

        if (specId is not null)
        {
            var specEntry = storedCharacter.SpecializationsSummary?.Specializations
                ?.FirstOrDefault(s => s.Specialization.Id == specId.Value);
            if (specEntry is null)
                return new BadRequestObjectResult(new { error = "Invalid specId: not found on character" });

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
                return new NotFoundObjectResult(new { error = "Run not found" });

            // 4b. Signup close time check — reject if signups are closed.
            if (RunEditability.IsEditingClosed(run.SignupCloseTime, run.StartTime, DateTimeOffset.UtcNow))
            {
                return new ObjectResult(new { error = "Signups are closed for this run" })
                { StatusCode = 409 };
            }

            // 4c. Visibility check for GUILD runs — mirrors runs-signup.ts.
            if (run.Visibility == "GUILD")
            {
                var isCreator = run.CreatorBattleNetId == principal.BattleNetId;
                var isGuildMember = callerGuildId is not null
                    && run.CreatorGuildId is not null
                    && run.CreatorGuildId.ToString() == callerGuildId;

                if (!isCreator && !isGuildMember)
                    return new NotFoundObjectResult(new { error = "Run not found" });

                var canSignup = await guildPermissions.CanSignupGuildRunsAsync(raider, ct);
                if (!canSignup)
                {
                    AuditLog.Emit(logger, new AuditEvent("signup.create", principal.BattleNetId, id, "failure", "guild rank denied"));
                    return new ObjectResult(new { error = "Guild signup is not enabled for your rank" })
                    { StatusCode = 403 };
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

            var reviewedAttendance = existingIndex >= 0
                ? run.RunCharacters[existingIndex].ReviewedAttendance
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
                var persisted = await runsRepo.UpdateAsync(updated, ct);

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
        return new ObjectResult(new { error = "Concurrent modification, please retry" })
        { StatusCode = 409 };
    }

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
            ModeKey: run.ModeKey,
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
