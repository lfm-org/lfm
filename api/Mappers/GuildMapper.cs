// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Guild;

namespace Lfm.Api.Mappers;

/// <summary>
/// Shared mapping from <see cref="GuildDocument"/> to <see cref="GuildDto"/>.
/// Used by GuildFunction and GuildAdminFunction.
/// </summary>
internal static class GuildMapper
{
    internal static GuildDto NoGuildDto() =>
        new(
            Guild: null,
            Setup: new GuildSetupDto(
                IsInitialized: false,
                RequiresSetup: true,
                RankDataFresh: false,
                Timezone: "UTC",
                Locale: "en"),
            Settings: null,
            Editor: new GuildEditorDto(CanEdit: false),
            MemberPermissions: new GuildMemberPermissionsDto(
                CanCreateGuildRuns: false,
                CanSignupGuildRuns: false,
                CanDeleteGuildRuns: false));

    internal static GuildDto MapToDto(GuildDocument doc, GuildEffectivePermissions permissions)
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
                RealmName: profile.Realm.Name ?? doc.RealmSlug,
                FactionName: profile.Faction?.Name,
                MemberCount: profile.MemberCount,
                RankCount: rankCount,
                CrestEmblemUrl: doc.CrestEmblemUrl,
                CrestBorderUrl: doc.CrestBorderUrl);
        }

        var setup = new GuildSetupDto(
            IsInitialized: doc.Setup?.InitializedAt is not null,
            RequiresSetup: false,
            RankDataFresh: IsRosterFresh(doc),
            Timezone: doc.Setup?.Timezone ?? "Europe/Helsinki",
            Locale: doc.Setup?.Locale ?? "fi");

        var editor = new GuildEditorDto(CanEdit: permissions.IsAdmin);

        var memberPermissions = new GuildMemberPermissionsDto(
            CanCreateGuildRuns: permissions.CanCreateGuildRuns,
            CanSignupGuildRuns: permissions.CanSignupGuildRuns,
            CanDeleteGuildRuns: permissions.CanDeleteGuildRuns);

        GuildSettingsDto? settings = null;
        if (permissions.IsAdmin)
        {
            var rankPerms = (doc.RankPermissions ?? Array.Empty<GuildRankPermission>())
                .Select(rp => new GuildRankPermissionDto(
                    Rank: rp.Rank,
                    CanCreateGuildRuns: rp.CanCreateGuildRuns,
                    CanSignupGuildRuns: rp.CanSignupGuildRuns,
                    CanDeleteGuildRuns: rp.CanDeleteGuildRuns))
                .ToList();
            settings = new GuildSettingsDto(RankPermissions: rankPerms);
        }

        return new GuildDto(
            Guild: guildInfo,
            Setup: setup,
            Settings: settings,
            Editor: editor,
            MemberPermissions: memberPermissions);
    }

    internal static bool IsRosterFresh(GuildDocument doc)
    {
        if (doc.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(doc.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        return DateTimeOffset.UtcNow - fetchedAt < TimeSpan.FromHours(1);
    }
}
