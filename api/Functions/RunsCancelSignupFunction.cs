using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves DELETE /api/runs/{id}/signup.
///
/// Lets an authenticated user cancel their signup for a run by removing their
/// <see cref="RunCharacterEntry"/> from the run document's <c>runCharacters</c> array.
///
/// Logic:
///   1. Load the run — 404 if not found.
///   2. For GUILD runs: check visibility access.
///   3. Find the user's entry in runCharacters by raiderBattleNetId.
///   4. 404 if the user is not signed up.
///   5. Remove the entry from the array.
///   6. Persist the run document.
///   7. Return the sanitized run.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-cancel-signup.ts</c>.
/// </summary>
public class RunsCancelSignupFunction(IRunsRepository runsRepo)
{
    [Function("runs-cancel-signup")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "runs/{id}/signup")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // 1. Load existing run.
        var run = await runsRepo.GetByIdAsync(id, ct);
        if (run is null)
            return new NotFoundObjectResult(new { error = "Run not found" });

        // 2. Visibility check for GUILD runs — mirrors runs-cancel-signup.ts:
        //    if (run.visibility === "GUILD" && !isCreator && !isGuildMember)
        //      return errorResponse(404, "Run not found");
        if (run.Visibility == "GUILD")
        {
            var isCreator = run.CreatorBattleNetId == principal.BattleNetId;
            var isGuildMember = principal.GuildId is not null
                && run.CreatorGuildId is not null
                && run.CreatorGuildId.ToString() == principal.GuildId;

            if (!isCreator && !isGuildMember)
                return new NotFoundObjectResult(new { error = "Run not found" });
        }

        // 3. Find the user's entry in runCharacters by raiderBattleNetId.
        var existingIndex = -1;
        for (var i = 0; i < run.RunCharacters.Count; i++)
        {
            if (run.RunCharacters[i].RaiderBattleNetId == principal.BattleNetId)
            {
                existingIndex = i;
                break;
            }
        }

        // 4. Return 404 if the user is not signed up.
        if (existingIndex < 0)
            return new NotFoundObjectResult(new { error = "No signup found" });

        // 5. Remove the entry from the array.
        var updatedCharacters = run.RunCharacters.ToList();
        updatedCharacters.RemoveAt(existingIndex);

        // 6. Persist the updated run document.
        var updated = run with { RunCharacters = updatedCharacters };
        var persisted = await runsRepo.UpdateAsync(updated, ct);

        // 7. Return sanitized run — mirrors sanitizeRunDocumentForResponse.
        return new OkObjectResult(Sanitize(persisted, principal.BattleNetId));
    }

    // ------------------------------------------------------------------
    // Sanitizer — mirrors sanitizeRunDocumentForResponse in
    // functions/src/lib/runResponseSanitizer.ts
    // ------------------------------------------------------------------

    private static RunDetailDto Sanitize(RunDocument run, string currentBattleNetId) =>
        new(
            Id: run.Id,
            StartTime: run.StartTime,
            SignupCloseTime: run.SignupCloseTime,
            Description: run.Description,
            ModeKey: run.ModeKey,
            Visibility: run.Visibility,
            CreatorGuild: run.CreatorGuild,
            CreatorGuildId: run.CreatorGuildId,
            InstanceId: run.InstanceId,
            InstanceName: run.InstanceName,
            CreatorBattleNetId: run.CreatorBattleNetId,
            CreatedAt: run.CreatedAt,
            Ttl: run.Ttl,
            RunCharacters: run.RunCharacters
                .Select(c => new RunCharacterDto(
                    Id: c.Id,
                    CharacterId: c.CharacterId,
                    CharacterName: c.CharacterName,
                    CharacterRealm: c.CharacterRealm,
                    CharacterLevel: c.CharacterLevel,
                    CharacterClassId: c.CharacterClassId,
                    CharacterClassName: c.CharacterClassName,
                    CharacterRaceId: c.CharacterRaceId,
                    CharacterRaceName: c.CharacterRaceName,
                    DesiredAttendance: c.DesiredAttendance,
                    ReviewedAttendance: c.ReviewedAttendance,
                    SpecId: c.SpecId,
                    SpecName: c.SpecName,
                    Role: c.Role,
                    IsCurrentUser: c.RaiderBattleNetId == currentBattleNetId))
                .ToList());
}
