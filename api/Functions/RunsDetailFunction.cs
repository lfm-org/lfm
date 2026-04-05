using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/runs/{id}.
///
/// Returns a single run by its Cosmos document id, sanitized so that each
/// RunCharacter's raiderBattleNetId is replaced by an IsCurrentUser flag.
///
/// Visibility rules (mirrors runs-detail.ts):
///   - The run is always returned if it is PUBLIC.
///   - The run is returned for GUILD visibility when:
///       * the requesting user is the creator, OR
///       * the requesting user belongs to the same guild as the creator.
///   - All other cases return 404 (no information leakage).
/// </summary>
public class RunsDetailFunction(IRunsRepository repo)
{
    [Function("runs-detail")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var run = await repo.GetByIdAsync(id, ct);
        if (run is null)
            return new NotFoundObjectResult(new { error = "Run not found" });

        // Visibility check — mirrors runs-detail.ts:
        //   if (resource.visibility === "GUILD" && !isCreator && !isGuildMember)
        //     return errorResponse(404, "Run not found");
        if (run.Visibility == "GUILD")
        {
            var isCreator = run.CreatorBattleNetId == principal.BattleNetId;
            var isGuildMember = principal.GuildId is not null
                && run.CreatorGuildId is not null
                && run.CreatorGuildId.ToString() == principal.GuildId;

            if (!isCreator && !isGuildMember)
                return new NotFoundObjectResult(new { error = "Run not found" });
        }

        return new OkObjectResult(Sanitize(run, principal.BattleNetId));
    }

    // ------------------------------------------------------------------
    // Sanitizer — mirrors sanitizeRunDocumentForResponse in
    // functions/src/lib/runResponseSanitizer.ts
    // ------------------------------------------------------------------

    private static RunDetailDto Sanitize(RunDocument run, string currentBattleNetId) =>
        new RunDetailDto(
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
                .Select(c => SanitizeCharacter(c, currentBattleNetId))
                .ToList());

    private static RunCharacterDto SanitizeCharacter(
        RunCharacterEntry character, string currentBattleNetId) =>
        new RunCharacterDto(
            Id: character.Id,
            CharacterId: character.CharacterId,
            CharacterName: character.CharacterName,
            CharacterRealm: character.CharacterRealm,
            CharacterLevel: character.CharacterLevel,
            CharacterClassId: character.CharacterClassId,
            CharacterClassName: character.CharacterClassName,
            CharacterRaceId: character.CharacterRaceId,
            CharacterRaceName: character.CharacterRaceName,
            DesiredAttendance: character.DesiredAttendance,
            ReviewedAttendance: character.ReviewedAttendance,
            SpecId: character.SpecId,
            SpecName: character.SpecName,
            Role: character.Role,
            IsCurrentUser: character.RaiderBattleNetId == currentBattleNetId);
}
