// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves POST /api/runs.
///
/// Creates a new run document. For GUILD visibility runs, the caller must have
/// the <c>canCreateGuildRuns</c> rank permission in their guild (defaults to
/// rank 0 only unless overridden by guild settings).
///
/// Server-side fields assigned here:
///   - id            — new GUID
///   - creatorBattleNetId — from the session principal
///   - creatorGuildId / creatorGuild — from the session principal
///   - status        — always "open" (for future use; not yet stored)
///   - createdAt     — UTC now
///   - ttl           — seconds until startTime + 7 days (minimum 1 day)
///   - runCharacters — always empty on creation
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-create.ts</c>.
/// </summary>
public class RunsCreateFunction(IRunsRepository repo, IRaidersRepository raidersRepo, IGuildPermissions guildPermissions, ILogger<RunsCreateFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private const long RunTtlAfterStartMs = 7L * 24 * 3600 * 1000;
    private const int MinTtlSeconds = 86400; // 1 day minimum

    [Function("runs-create")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "runs")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var body = await JsonSerializer.DeserializeAsync<CreateRunRequest>(
            req.Body,
            JsonOptions,
            cancellationToken: ct);

        if (body is null)
            return new BadRequestObjectResult(new { error = "invalid body" });

        var validator = new CreateRunRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(
                new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // Load the raider once and derive guild info from the selected character.
        // principal.GuildId / GuildName are legacy session fields that are no
        // longer populated in production.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new NotFoundObjectResult(new { error = "Raider not found" });

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        // GUILD run guard: mirroring the checks in runs-create.ts.
        //   1. GUILD run requires the caller to belong to a guild.
        //   2. GUILD run requires the canCreateGuildRuns rank permission.
        if (body.Visibility == "GUILD")
        {
            if (guildId is null)
                return new BadRequestObjectResult(
                    new { error = "A guild run requires an active character in a guild" });

            var canCreate = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canCreate)
            {
                AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, null, "failure", "guild rank denied"));
                return new ObjectResult(new { error = "Guild run creation is not enabled for your rank" })
                { StatusCode = 403 };
            }
        }

        var runId = Guid.NewGuid().ToString();
        var createdAt = DateTimeOffset.UtcNow.ToString("o");

        var run = BuildRunDocument(body, principal, guildId, guildName, runId, createdAt);
        var created = await repo.CreateAsync(run, ct);

        AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, runId, "success", null));

        return new ObjectResult(MapToDto(created)) { StatusCode = 201 };
    }

    // ------------------------------------------------------------------
    // Document builder — mirrors buildRunDocument in runs-create.ts
    // ------------------------------------------------------------------

    internal static RunDocument BuildRunDocument(
        CreateRunRequest body,
        SessionPrincipal principal,
        string? guildId,
        string? guildName,
        string id,
        string createdAt)
    {
        var startTimeMs = DateTimeOffset.TryParse(body.StartTime, out var st)
            ? st.ToUnixTimeMilliseconds()
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var createdAtMs = DateTimeOffset.TryParse(createdAt, out var ca)
            ? ca.ToUnixTimeMilliseconds()
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var expiryMs = startTimeMs + RunTtlAfterStartMs;
        var ttl = (int)Math.Max(MinTtlSeconds, (expiryMs - createdAtMs) / 1000);

        int? creatorGuildId = guildId is not null
            && int.TryParse(guildId, out var gid)
            ? gid
            : null;

        return new RunDocument(
            Id: id,
            StartTime: body.StartTime!,
            SignupCloseTime: body.SignupCloseTime ?? "",
            Description: body.Description ?? "",
            ModeKey: body.ModeKey!,
            Visibility: body.Visibility!,
            CreatorGuild: guildName ?? "",
            CreatorGuildId: creatorGuildId,
            InstanceId: body.InstanceId!.Value,
            InstanceName: body.InstanceName ?? "",
            CreatorBattleNetId: principal.BattleNetId,
            CreatedAt: createdAt,
            Ttl: ttl,
            RunCharacters: []);
    }

    // ------------------------------------------------------------------
    // Mapping helper — reuses RunDetailDto (same shape as the run doc).
    // ------------------------------------------------------------------

    private static RunDetailDto MapToDto(RunDocument doc) =>
        new(
            Id: doc.Id,
            StartTime: doc.StartTime,
            SignupCloseTime: doc.SignupCloseTime,
            Description: doc.Description,
            ModeKey: doc.ModeKey,
            Visibility: doc.Visibility,
            CreatorGuild: doc.CreatorGuild,
            CreatorGuildId: doc.CreatorGuildId,
            InstanceId: doc.InstanceId,
            InstanceName: doc.InstanceName,
            CreatorBattleNetId: doc.CreatorBattleNetId,
            CreatedAt: doc.CreatedAt,
            Ttl: doc.Ttl,
            RunCharacters: []);
}
