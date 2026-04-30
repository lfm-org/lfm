// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services.Blizzard.Models;

namespace Lfm.Api.Services.Blizzard;

/// <summary>
/// Translates Blizzard HTTP response models (<see cref="Models"/>) into the
/// persistence shapes (<c>Stored*</c>) we store in Cosmos. Owned by the Blizzard
/// adapter so any future Blizzard schema change is contained at this boundary,
/// not in repositories or Function handlers.
///
/// Resolves finding SD-S-5 from
/// docs/superpowersreviews/2026-04-29-software-design-deep-review.md.
/// </summary>
internal static class BlizzardModelTranslator
{
    // ---------- Account profile ----------

    public static StoredBlizzardAccountProfile ToStored(AccountProfileSummaryResponse src) =>
        new(WowAccounts: src.WowAccounts?.Select(ToStored).ToList());

    private static StoredBlizzardWowAccount ToStored(WowAccountResponse src) =>
        new(Id: src.Id, Characters: src.Characters?.Select(ToStored).ToList());

    private static StoredBlizzardAccountCharacter ToStored(AccountCharacterResponse src) =>
        new(
            Name: src.Name,
            Level: src.Level,
            Realm: ToStored(src.Realm),
            PlayableClass: src.PlayableClass is null ? null : ToStored(src.PlayableClass));

    private static StoredBlizzardRealmRef ToStored(RealmRefResponse src) =>
        new(Slug: src.Slug, Name: src.Name);

    private static StoredBlizzardNamedRef ToStored(NamedRefResponse src) =>
        new(Id: src.Id, Name: src.Name);

    // ---------- Character media ----------

    public static StoredBlizzardCharacterMedia ToStored(CharacterMediaSummaryResponse src) =>
        new(Assets: src.Assets?
            .Select(a => new StoredBlizzardCharacterMediaAsset(a.Key, a.Value))
            .ToList());

    // ---------- Guild roster ----------

    public static StoredGuildRoster ToStored(GuildRosterResponse src) =>
        new(Members: src.Members?.Select(ToStored).ToList());

    private static StoredGuildRosterMember ToStored(GuildRosterMemberResponse src) =>
        new(Character: ToStored(src.Character), Rank: src.Rank);

    private static StoredGuildRosterMemberCharacter ToStored(GuildRosterMemberCharacterResponse src) =>
        new(Name: src.Name, Realm: ToStored(src.Realm), Id: src.Id);

    private static StoredGuildRosterRealm ToStored(GuildRosterRealmResponse src) =>
        new(Slug: src.Slug);

    // ---------- Guild profile ----------

    public static StoredGuildProfile ToStored(GuildProfileResponse src) => new(
        Name: src.Name,
        Realm: ToStored(src.Realm),
        Faction: src.Faction is null ? null : ToStored(src.Faction),
        MemberCount: src.MemberCount,
        AchievementPoints: src.AchievementPoints);

    private static StoredGuildProfileRealm ToStored(GuildProfileRealmResponse src) =>
        new(Slug: src.Slug, Name: src.Name);

    private static StoredGuildProfileFaction ToStored(GuildProfileFactionResponse src) =>
        new(Name: src.Name);
}
