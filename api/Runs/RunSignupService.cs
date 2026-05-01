// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;
using Lfm.Contracts.WoW;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Runs;

/// <summary>
/// Implements the run-signup policy: load the raider, verify character
/// ownership, and run a read-modify-write loop with optimistic concurrency
/// that loads the run, gates every signup on guild view + canSignupGuildRuns
/// rank permission, upserts the <see cref="RunCharacterEntry"/> (handling the
/// rejection-list IN->OUT default flip), and persists the document.
///
/// Returns a <see cref="RunOperationResult"/> that the Function adapter
/// translates to HTTP. Audit emission for the success and forbidden paths
/// stays at the Function — same pattern as <see cref="RunCreateService"/>
/// and <see cref="RunUpdateService"/>.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-signup.ts</c>.
/// </summary>
public sealed class RunSignupService(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IGuildPermissions guildPermissions,
    ILogger<RunSignupService> logger) : IRunSignupService
{
    private const int MaxAttempts = 3;

    public async Task<RunOperationResult> SignupAsync(
        string runId,
        SignupRequest body,
        SessionPrincipal principal,
        CancellationToken ct)
    {
        // 1. Load raider document and verify character ownership.
        //    Mirrors: const storedCharacter = raider.characters.find(c => c.id === body.characterId)
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new RunOperationResult.NotFound("raider-not-found", "Raider not found.");

        // Derive the caller's guild from the raider's selected character for the
        // guild-only visibility check below. principal.GuildId is a legacy session
        // field and is no longer populated.
        var (callerGuildId, _) = GuildResolver.FromRaider(raider);

        var storedCharacter = raider.Characters?.FirstOrDefault(c => c.Id == body.CharacterId);
        if (storedCharacter is null)
            return new RunOperationResult.BadRequest(
                "character-not-on-profile",
                "Character not found on your profile.");

        // 2. Resolve spec info — mirrors the specId block in runs-signup.ts.
        int? specId = body.SpecId;
        string? specName = null;
        string? role = null;

        if (specId is not null)
        {
            var specEntry = storedCharacter.SpecializationsSummary?.Specializations
                ?.FirstOrDefault(s => s.Specialization.Id == specId.Value);
            if (specEntry is null)
                return new RunOperationResult.BadRequest(
                    "invalid-spec-id",
                    "Invalid specId: not found on character.");

            specName = specEntry.Specialization.Name;
        }

        // 3. Read-modify-write loop with optimistic concurrency.
        //    On ConcurrencyConflictException the loop re-reads the run and retries.
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // 3a. Load existing run.
            var run = await runsRepo.GetByIdAsync(runId, ct);
            if (run is null)
                return new RunOperationResult.NotFound("run-not-found", "Run not found.");

            // 3b. Signup close time check — reject if signups are closed.
            if (RunEditability.IsEditingClosed(run.SignupCloseTime, run.StartTime, DateTimeOffset.UtcNow))
            {
                return new RunOperationResult.ConflictResult(
                    "signups-closed",
                    "Signups are closed for this run.");
            }

            // 3c. Guild-only visibility and rank checks.
            if (!RunAccessPolicy.CanView(run, principal.BattleNetId, callerGuildId))
                return new RunOperationResult.NotFound("run-not-found", "Run not found.");

            var canSignup = await guildPermissions.CanSignupGuildRunsAsync(raider, ct);
            if (!canSignup)
            {
                return new RunOperationResult.Forbidden(
                    "guild-rank-denied",
                    "Guild signup is not enabled for your rank.",
                    AuditReason: "guild rank denied");
            }

            // 3d. Upsert the RunCharacterEntry — one per battleNetId per run.
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
            // "OUT" to close the cancel-then-resignup bypass.
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

            // 3e. Persist the updated run document with ETag check.
            var updated = run with { RunCharacters = updatedCharacters };
            try
            {
                // Pass null so the repository falls back to run.ETag — this is an
                // internal retry loop, not a client-driven If-Match flow.
                var persisted = await runsRepo.UpdateAsync(updated, ifMatchEtag: null, ct);
                return new RunOperationResult.Ok(persisted);
            }
            catch (ConcurrencyConflictException)
            {
                logger.LogWarning(
                    "Concurrency conflict on signup for run {RunId}, attempt {Attempt}",
                    runId,
                    attempt + 1);
                // Loop will re-read the document and retry.
            }
        }

        // All retry attempts exhausted.
        return new RunOperationResult.ConflictResult(
            "concurrent-modification",
            "Concurrent modification, please retry.");
    }
}
