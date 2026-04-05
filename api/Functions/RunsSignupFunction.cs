using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;

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
    IGuildPermissions guildPermissions)
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

        // 1. Load existing run.
        var run = await runsRepo.GetByIdAsync(id, ct);
        if (run is null)
            return new NotFoundObjectResult(new { error = "Run not found" });

        // 2. Visibility check for GUILD runs — mirrors runs-signup.ts:
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

            // canSignupGuildRuns permission check — mirrors runs-signup.ts guild block.
            var canSignup = await guildPermissions.CanSignupGuildRunsAsync(principal, ct);
            if (!canSignup)
                return new ObjectResult(new { error = "Guild signup is not enabled for your rank" })
                    { StatusCode = 403 };
        }

        // 3. Parse and validate request body.
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
        catch (JsonException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }

        var validator = new SignupRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(
                new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // 4. Load raider document and verify character ownership.
        //    Mirrors: const storedCharacter = raider.characters.find(c => c.id === body.characterId)
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new NotFoundObjectResult(new { error = "Raider not found" });

        var storedCharacter = raider.Characters?.FirstOrDefault(c => c.Id == body.CharacterId);
        if (storedCharacter is null)
            return new BadRequestObjectResult(new { error = "Character not found on your profile" });

        // 5. Resolve spec info — mirrors the specId block in runs-signup.ts.
        //    We validate specId against the character's stored specializations when available.
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
            // Role is not stored in StoredSpecializationsEntry; leave null (static spec map not ported).
        }

        // 6. Upsert the RunCharacterEntry — one per battleNetId per run.
        //    Mirrors the existingIndex / upsert pattern in runs-signup.ts.
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

        // Character demographic fields (level, classId, raceId, class/race names) are not
        // stored in the .NET StoredSelectedCharacter model (profileSummary not modelled).
        // We use safe defaults; these fields are cosmetic in the run document.
        var entry = new RunCharacterEntry(
            Id: entryId,
            CharacterId: storedCharacter.Id,
            CharacterName: storedCharacter.Name,
            CharacterRealm: storedCharacter.Realm,
            CharacterLevel: 0,
            CharacterClassId: 0,
            CharacterClassName: "",
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

        // 7. Persist the updated run document.
        var updated = run with { RunCharacters = updatedCharacters };
        var persisted = await runsRepo.UpdateAsync(updated, ct);

        // 8. Return sanitized run — mirrors sanitizeOptionalRunDocumentForResponse.
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
