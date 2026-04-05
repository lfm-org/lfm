using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/runs.
///
/// Returns all runs visible to the authenticated user, sanitized so that each
/// RunCharacter's raiderBattleNetId is replaced by an IsCurrentUser flag.
///
/// Visibility rules (mirrors runs-list.ts):
///   - For users with a guild: PUBLIC runs, runs created by the user, and GUILD
///     runs created by the same guild.
///   - For users without a guild: PUBLIC runs and runs created by the user.
///
/// Results are ordered by startTime ascending.
/// </summary>
public class RunsListFunction(IRunsRepository repo)
{
    [Function("runs-list")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        IReadOnlyList<RunDocument> runs = principal.GuildId is not null
            ? await repo.ListForGuildAsync(principal.GuildId, principal.BattleNetId, ct)
            : await repo.ListForUserAsync(principal.BattleNetId, ct);

        var dtos = runs
            .Select(r => Sanitize(r, principal.BattleNetId))
            .ToList();

        return new OkObjectResult(dtos);
    }

    // ------------------------------------------------------------------
    // Sanitizer — mirrors sanitizeRunDocumentForResponse in
    // functions/src/lib/runResponseSanitizer.ts
    // ------------------------------------------------------------------

    private static RunSummaryDto Sanitize(RunDocument run, string currentBattleNetId) =>
        new RunSummaryDto(
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
