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
using Lfm.Contracts.Guild;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/guild and PATCH /api/guild.
///
/// GET  — returns the guild home view for the current user's guild.
///         Returns 404 when the principal has no associated guild (GuildId is null)
///         or the guild document does not exist in Cosmos.
///         Mirrors the TypeScript <c>currentGuildHandler</c> in
///         <c>functions/src/functions/guild.ts</c>.
///
/// PATCH — updates guild settings (timezone, locale, slogan, rankPermissions).
///         Requires the caller to be a guild admin (rank 0 in the Blizzard roster).
///         Returns 403 when the caller is not an admin.
///         Mirrors the TypeScript <c>saveCurrentGuildSettings</c> logic in
///         <c>functions/src/lib/guild/service.ts</c>.
/// </summary>
public class GuildFunction(IGuildRepository guildRepo, IGuildPermissions guildPermissions, ILogger<GuildFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // ------------------------------------------------------------------
    // GET /api/guild
    // ------------------------------------------------------------------

    [Function("guild-get")]
    [RequireAuth]
    public async Task<IActionResult> GuildGet(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "guild")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        if (principal.GuildId is null)
            return new NotFoundResult();

        var guildDoc = await guildRepo.GetAsync(principal.GuildId, cancellationToken);
        if (guildDoc is null)
            return new NotFoundResult();

        return new OkObjectResult(MapToDto(guildDoc));
    }

    // ------------------------------------------------------------------
    // PATCH /api/guild
    // ------------------------------------------------------------------

    [Function("guild-update")]
    [RequireAuth]
    public async Task<IActionResult> GuildUpdate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "guild")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        if (principal.GuildId is null)
            return new NotFoundResult();

        var isAdmin = await guildPermissions.IsAdminAsync(principal, cancellationToken);
        if (!isAdmin)
            return new ObjectResult(new { error = "forbidden" }) { StatusCode = 403 };

        var body = await JsonSerializer.DeserializeAsync<UpdateGuildRequest>(
            req.Body,
            JsonOptions,
            cancellationToken: cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "invalid body" });

        var validator = new UpdateGuildRequestValidator();
        var validationResult = await validator.ValidateAsync(body, cancellationToken);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(
                new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        var guildDoc = await guildRepo.GetAsync(principal.GuildId, cancellationToken);
        if (guildDoc is null)
            return new NotFoundResult();

        var updatedSetup = guildDoc.Setup is not null
            ? guildDoc.Setup with
            {
                InitializedAt = guildDoc.Setup.InitializedAt ?? DateTimeOffset.UtcNow.ToString("o"),
                Timezone = body.Timezone!,
                Locale = body.Locale!,
            }
            : new GuildSetup(
                InitializedAt: DateTimeOffset.UtcNow.ToString("o"),
                Timezone: body.Timezone!,
                Locale: body.Locale!);

        var updatedRankPermissions = body.RankPermissions is not null
            ? body.RankPermissions
                .Select(rp => new GuildRankPermission(rp.Rank, rp.CanCreateGuildRuns, rp.CanSignupGuildRuns, rp.CanDeleteGuildRuns))
                .ToList()
            : guildDoc.RankPermissions;

        var updatedDoc = guildDoc with
        {
            Slogan = body.Slogan ?? guildDoc.Slogan,
            Setup = updatedSetup,
            RankPermissions = updatedRankPermissions,
        };

        await guildRepo.UpsertAsync(updatedDoc, cancellationToken);

        AuditLog.Emit(logger, new AuditEvent("guild.update", principal.BattleNetId, principal.GuildId, "success", null));

        return new OkObjectResult(MapToDto(updatedDoc));
    }

    // ------------------------------------------------------------------
    // Mapping helpers
    // ------------------------------------------------------------------

    private static GuildDto MapToDto(GuildDocument doc)
    {
        var profile = doc.BlizzardProfileRaw;

        GuildInfoDto? guildInfo = null;
        if (profile is not null)
        {
            var rosterMembers = doc.BlizzardRosterRaw?.Members;
            var rankCount = rosterMembers is not null
                ? rosterMembers.Select(m => m.Rank).Distinct().Count()
                : (int?)null;

            guildInfo = new GuildInfoDto(
                Id: doc.GuildId,
                Name: profile.Name,
                Slogan: doc.Slogan,
                RealmSlug: doc.RealmSlug,
                RealmName: profile.Realm.Name ?? doc.RealmSlug,
                FactionName: profile.Faction?.Name,
                MemberCount: profile.MemberCount,
                AchievementPoints: profile.AchievementPoints,
                SyncedMemberCount: rosterMembers?.Count,
                RankCount: rankCount,
                CrestEmblemUrl: doc.CrestEmblemUrl,
                CrestBorderUrl: doc.CrestBorderUrl);
        }

        var setup = new GuildSetupDto(
            IsInitialized: doc.Setup?.InitializedAt is not null,
            RequiresSetup: false,
            RankDataFresh: IsRosterFresh(doc),
            RankDataFetchedAt: doc.BlizzardRosterFetchedAt,
            Timezone: doc.Setup?.Timezone ?? "Europe/Helsinki",
            Locale: doc.Setup?.Locale ?? "fi");

        var editor = new GuildEditorDto(CanEdit: false, Mode: "member");

        var memberPermissions = new GuildMemberPermissionsDto(
            MatchedRank: null,
            CanCreateGuildRuns: false,
            CanSignupGuildRuns: false,
            CanDeleteGuildRuns: false,
            RankDataFresh: IsRosterFresh(doc));

        return new GuildDto(
            Guild: guildInfo,
            Setup: setup,
            Settings: null,
            Editor: editor,
            MemberPermissions: memberPermissions);
    }

    private static bool IsRosterFresh(GuildDocument doc)
    {
        if (doc.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(doc.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        return DateTimeOffset.UtcNow - fetchedAt < TimeSpan.FromHours(1);
    }
}
