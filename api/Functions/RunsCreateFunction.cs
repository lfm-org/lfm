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
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");

        var validator = new CreateRunRequestValidator();
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

        // Load the raider once and derive guild info from the selected character.
        // principal.GuildId / GuildName are legacy session fields that are no
        // longer populated in production.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        // GUILD run guard: mirroring the checks in runs-create.ts.
        //   1. GUILD run requires the caller to belong to a guild.
        //   2. GUILD run requires the canCreateGuildRuns rank permission.
        if (body.Visibility == "GUILD")
        {
            if (guildId is null)
                return Problem.BadRequest(
                    req.HttpContext,
                    "guild-required",
                    "A guild run requires an active character in a guild.");

            var canCreate = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
            if (!canCreate)
            {
                AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, null, "failure", "guild rank denied"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "guild-rank-denied",
                    "Guild run creation is not enabled for your rank.");
            }
        }

        var runId = Guid.NewGuid().ToString();
        var createdAt = DateTimeOffset.UtcNow.ToString("o");

        var run = BuildRunDocument(body, principal, guildId, guildName, runId, createdAt);
        var created = await repo.CreateAsync(run, ct);

        AuditLog.Emit(logger, new AuditEvent("run.create", principal.BattleNetId, runId, "success", null));

        return new ObjectResult(MapToDto(created)) { StatusCode = 201 };
    }

    /// <summary>
    /// <c>/api/v1/runs</c> POST alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-create-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/runs")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, ctx, ct);

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

        // The validator guarantees Difficulty + Size are populated; ModeKey
        // is computed from them so the persisted RunDocument still satisfies
        // any legacy reader on the read side (storage-only compatibility —
        // the wire no longer carries ModeKey).
        var difficulty = body.Difficulty!;
        var size = body.Size!.Value;
        var modeKey = $"{difficulty}:{size}";

        return new RunDocument(
            Id: id,
            StartTime: body.StartTime!,
            SignupCloseTime: body.SignupCloseTime ?? "",
            Description: body.Description ?? "",
            ModeKey: modeKey,
            Visibility: body.Visibility!,
            CreatorGuild: guildName ?? "",
            CreatorGuildId: creatorGuildId,
            InstanceId: body.InstanceId,
            InstanceName: body.InstanceName,
            CreatorBattleNetId: principal.BattleNetId,
            CreatedAt: createdAt,
            Ttl: ttl,
            RunCharacters: [],
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: body.KeystoneLevel);
    }

    // ------------------------------------------------------------------
    // Mapping helper — projects the stored RunDocument to its wire DTO.
    // ------------------------------------------------------------------

    private static RunDetailDto MapToDto(RunDocument doc)
    {
        var (difficulty, size) = RunModeResolver.Resolve(doc.Difficulty, doc.Size, doc.ModeKey);
        return new RunDetailDto(
            Id: doc.Id,
            StartTime: doc.StartTime,
            SignupCloseTime: doc.SignupCloseTime,
            Description: doc.Description,
            Visibility: doc.Visibility,
            CreatorGuild: doc.CreatorGuild,
            InstanceId: doc.InstanceId,
            InstanceName: doc.InstanceName,
            RunCharacters: [],
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: doc.KeystoneLevel);
    }
}
