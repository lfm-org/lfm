// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Implements the run-create policy: load the raider, derive guild context,
/// gate run creation on the <c>canCreateGuildRuns</c> rank permission, build
/// the guild-scoped Cosmos document with server-assigned fields, and persist
/// it. Returns a <see cref="RunOperationResult"/> that the Function adapter
/// translates to HTTP.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-create.ts</c>.
/// </summary>
public sealed class RunCreateService(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IGuildPermissions guildPermissions,
    ISiteAdminService siteAdmin) : IRunCreateService
{
    private const long RunTtlAfterStartMs = 7L * 24 * 3600 * 1000;
    private const int MinTtlSeconds = 86400; // 1 day minimum

    public async Task<RunOperationResult> CreateAsync(
        CreateRunRequest body,
        SessionPrincipal principal,
        CancellationToken ct)
    {
        // Load the raider once and derive guild info from the selected character.
        // principal.GuildId / GuildName are legacy session fields that are no
        // longer populated in production.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new RunOperationResult.NotFound("raider-not-found", "Raider not found.");

        var (guildId, guildName) = GuildResolver.FromRaider(raider);

        if (guildId is null)
            return new RunOperationResult.BadRequest(
                "guild-required",
                "A run requires an active character in a guild.");

        var canCreate = await guildPermissions.CanCreateGuildRunsAsync(raider, ct);
        if (!canCreate && !await siteAdmin.IsAdminAsync(principal.BattleNetId, ct))
            return new RunOperationResult.Forbidden(
                "guild-rank-denied",
                "Guild run creation is not enabled for your rank.",
                AuditReason: "guild rank denied");

        var runId = Guid.NewGuid().ToString();
        var createdAt = DateTimeOffset.UtcNow.ToString("o");

        var run = BuildRunDocument(body, principal, guildId, guildName, runId, createdAt);
        var created = await runsRepo.CreateAsync(run, ct);

        return new RunOperationResult.Ok(created);
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
            Visibility: "GUILD",
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
}
