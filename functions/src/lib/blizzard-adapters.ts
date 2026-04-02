import { resolveSpecRole } from "./wowSpecRoles.js";
import { getResolvedRankPermissions, isGuildRosterFresh, type EffectiveGuildPermissions } from "./guild-permissions.js";
import { getServedCharacterPortraitUrl, isBlizzardRenderUrl } from "./character-portrait.js";
import type {
  BlizzardAccountGuildsSummary,
  BlizzardAccountProfileSummary,
  BlizzardCharacterProfileSummary,
  BlizzardCharacterSpecializationsSummary,
  BlizzardGuildProfileResponse,
  BlizzardGuildRosterResponse,
  BlizzardJournalInstanceIndexResponse,
  BlizzardJournalInstanceResponse,
  BlizzardLocalizedString,
  BlizzardPlayableClassIndexResponse,
  BlizzardPlayableClassResponse,
  BlizzardPlayableRaceIndexResponse,
  BlizzardPlayableRaceResponse,
  BlizzardPlayableSpecializationIndexResponse,
  BlizzardPlayableSpecializationResponse,
} from "../types/blizzard.js";
import type {
  AccountCharacter,
  BattleNetIdentity,
  Character,
  GuildDocument,
  StoredSelectedCharacter,
  WowClass,
  WowInstance,
  WowRace,
  WowSpecialization,
} from "../types/index.js";

function localizeName(value?: string | BlizzardLocalizedString): string {
  if (typeof value === "string") return value;
  return value?.en_US ?? value?.en_GB ?? "";
}

function toRole(type?: "DAMAGE" | "HEALER" | "TANK"): "TANK" | "HEALER" | "DPS" {
  if (type === "HEALER" || type === "TANK") return type;
  return "DPS";
}

export function toWowClassViews(
  index: BlizzardPlayableClassIndexResponse,
  details: Map<number, BlizzardPlayableClassResponse>
): WowClass[] {
  return index.classes.flatMap((entry) => {
    const detail = details.get(entry.id);
    return detail ? [{ id: detail.id, name: detail.name }] : [];
  });
}

export function toWowRaceViews(
  index: BlizzardPlayableRaceIndexResponse,
  details: Map<number, BlizzardPlayableRaceResponse>
): WowRace[] {
  return index.races.flatMap((entry) => {
    const detail = details.get(entry.id);
    return detail ? [{ id: detail.id, faction: detail.faction?.type ?? "UNKNOWN", name: detail.name }] : [];
  });
}

export function toWowSpecializationViews(
  index: BlizzardPlayableSpecializationIndexResponse,
  details: Map<number, BlizzardPlayableSpecializationResponse>
): WowSpecialization[] {
  return index.character_specializations.flatMap((entry) => {
    const detail = details.get(entry.id);
    return detail
      ? [{
          id: detail.id,
          name: detail.name,
          classId: detail.playable_class.id,
          role: toRole(detail.role.type),
        }]
      : [];
  });
}

export function toWowInstanceViews(
  index: BlizzardJournalInstanceIndexResponse,
  details: Map<number, BlizzardJournalInstanceResponse>
): WowInstance[] {
  return index.instances.flatMap((entry) => {
    const detail = details.get(entry.id);
    return detail
      ? [{
          id: detail.id,
          name: localizeName(detail.name) || localizeName(entry.name),
          type: detail.category?.type ?? "UNKNOWN",
          minLevel: detail.minimum_level ?? 0,
          expansionId: detail.expansion?.id ?? 0,
          modes: detail.modes?.map((mode) => ({
            mode: {
              type: mode.mode.type,
              name: localizeName(mode.mode.name) || mode.mode.type,
            },
            ...(mode.players !== undefined ? { players: mode.players } : {}),
            ...(mode.is_tracked !== undefined ? { is_tracked: mode.is_tracked } : {}),
          })) ?? [],
        }]
      : [];
  });
}

export function toAccountCharacterViews(
  summary: BlizzardAccountProfileSummary,
  region: string,
  storedCharacters?: StoredSelectedCharacter[],
  portraitCache?: Record<string, string>
): AccountCharacter[] {
  const storedByKey = new Map<string, StoredSelectedCharacter>();
  for (const sc of storedCharacters ?? []) {
    storedByKey.set(`${sc.name.toLowerCase()}:${sc.realm.toLowerCase()}`, sc);
  }

  return (summary.wow_accounts ?? []).flatMap((account) =>
    (account.characters ?? []).map((character) => {
      const stored = storedByKey.get(`${character.name.toLowerCase()}:${character.realm.slug.toLowerCase()}`);
      const classId = stored?.profileSummary?.character_class?.id ?? character.playable_class?.id;
      const className = localizeName(stored?.profileSummary?.character_class?.name) || undefined;
      const cachedId = `${region}-${character.realm.slug}-${character.name.toLowerCase()}`;
      const cachedPortraitUrl = portraitCache?.[cachedId];
      const portraitUrl = stored
        ? getServedCharacterPortraitUrl(stored.id, stored.portraitUrl, stored.portraitBlobName)
        : (cachedPortraitUrl ? getServedCharacterPortraitUrl(cachedId, cachedPortraitUrl) : "");
      const safePortraitUrl = portraitUrl && !isBlizzardRenderUrl(portraitUrl) ? portraitUrl : undefined;
      const activeSpecId = stored?.specializationsSummary?.active_specialization?.id ?? null;
      const activeSpec = stored?.specializationsSummary?.specializations?.find(
        s => s.specialization.id === activeSpecId
      );
      return {
        name: character.name,
        realm: character.realm.slug,
        realmName: localizeName(character.realm.name) || character.realm.slug,
        level: character.level,
        region,
        ...(classId !== undefined ? { classId } : {}),
        ...(className ? { className } : {}),
        ...(safePortraitUrl ? { portraitUrl: safePortraitUrl } : {}),
        ...(activeSpecId !== null ? { activeSpecId } : {}),
        ...(activeSpec ? { specName: localizeName(activeSpec.specialization.name) || null } : {}),
      };
    })
  );
}

export function toBattleNetIdentity(
  battleNetId: string,
  selectedCharacter?: StoredSelectedCharacter | null,
  accountGuildsSummary?: BlizzardAccountGuildsSummary | null,
): BattleNetIdentity {
  const guild = selectedCharacter?.profileSummary?.guild ?? accountGuildsSummary?.guilds?.[0]?.guild;
  return {
    battleNetId,
    guildId: guild?.id ?? null,
    guildName: guild?.name ?? null,
  };
}

function toSpecializations(
  specializationsSummary: BlizzardCharacterSpecializationsSummary | null | undefined,
  staticSpecs: Map<number, WowSpecialization>
): NonNullable<Character["specializations"]> {
  return (specializationsSummary?.specializations ?? []).map((entry) => ({
    id: entry.specialization.id,
    name: localizeName(entry.specialization.name),
    role: staticSpecs.get(entry.specialization.id)?.role ?? resolveSpecRole(entry.specialization.id),
  }));
}

export interface GuildHomeView {
  guild: {
    id: number;
    name: string;
    slogan: string | null;
    realmSlug: string;
    realmName: string;
    factionName: string | null;
    memberCount: number | null;
    achievementPoints: number | null;
    syncedMemberCount: number | null;
    rankCount: number | null;
    crestEmblemUrl: string | null;
    crestBorderUrl: string | null;
  } | null;
  setup: {
    isInitialized: boolean;
    requiresSetup: boolean;
    rankDataFresh: boolean;
    rankDataFetchedAt: string | null;
    timezone: string;
    locale: string;
  };
  settings: {
    rankPermissions: Array<{
      rank: number;
      canCreateGuildRaids: boolean;
      canSignupGuildRaids: boolean;
      canDeleteGuildRaids: boolean;
    }>;
  } | null;
  editor: {
    canEdit: boolean;
    mode: "member" | "guild-master" | "site-admin";
    overrideAvailable: false;
  };
  memberPermissions: EffectiveGuildPermissions;
  adminOverride: {
    lastOverrideBy: string | null;
    lastOverrideAt: string | null;
  } | null;
}

function getGuildProfile(guildDoc: GuildDocument): BlizzardGuildProfileResponse | undefined {
  return guildDoc.blizzardProfileRaw ?? guildDoc.profileSummary;
}

function getGuildRoster(guildDoc: GuildDocument): BlizzardGuildRosterResponse | undefined {
  return guildDoc.blizzardRosterRaw;
}

export function toGuildHomeView(
  guildDoc?: GuildDocument | null,
  editor: GuildHomeView["editor"] = { canEdit: false, mode: "member", overrideAvailable: false },
  memberPermissions: GuildHomeView["memberPermissions"] = {
    matchedRank: null,
    canCreateGuildRaids: false,
    canSignupGuildRaids: false,
    canDeleteGuildRaids: false,
    rankDataFresh: false,
  },
  adminOverride: GuildHomeView["adminOverride"] = null
): GuildHomeView {
  const profile = guildDoc ? getGuildProfile(guildDoc) : undefined;
  const roster = guildDoc ? getGuildRoster(guildDoc) : undefined;
  const rankDataFresh = isGuildRosterFresh(guildDoc);

  if (!guildDoc || !profile) {
    return {
      guild: null,
      setup: {
        isInitialized: false,
        requiresSetup: false,
        rankDataFresh,
        rankDataFetchedAt: guildDoc?.blizzardRosterFetchedAt ?? null,
        timezone: guildDoc?.setup?.timezone ?? "Europe/Helsinki",
        locale: guildDoc?.setup?.locale ?? "fi",
      },
      settings: null,
      editor: {
        ...editor,
      },
      memberPermissions,
      adminOverride,
    };
  }

  return {
    guild: {
      id: guildDoc.guildId,
      name: profile.name,
      slogan: guildDoc.slogan ?? null,
      realmSlug: guildDoc.realmSlug,
      realmName: localizeName(profile.realm.name) || guildDoc.realmSlug,
      factionName: localizeName(profile.faction?.name) || null,
      memberCount: profile.member_count ?? null,
      achievementPoints: profile.achievement_points ?? null,
      syncedMemberCount: roster?.members.length ?? null,
      rankCount: roster ? new Set(roster.members.map((member) => member.rank)).size : null,
      crestEmblemUrl: guildDoc.crestEmblemUrl ?? null,
      crestBorderUrl: guildDoc.crestBorderUrl ?? null,
    },
    setup: {
      isInitialized: Boolean(guildDoc.setup?.initializedAt),
      requiresSetup: editor.canEdit && !guildDoc.setup?.initializedAt,
      rankDataFresh,
      rankDataFetchedAt: guildDoc.blizzardRosterFetchedAt ?? null,
      timezone: guildDoc.setup?.timezone ?? "Europe/Helsinki",
      locale: guildDoc.setup?.locale ?? "fi",
    },
    settings: editor.canEdit
      ? {
          rankPermissions: getResolvedRankPermissions(guildDoc),
        }
      : null,
    editor: {
      ...editor,
    },
    memberPermissions,
    adminOverride: adminOverride ?? {
      lastOverrideBy: guildDoc.lastOverrideBy ?? null,
      lastOverrideAt: guildDoc.lastOverrideAt ?? null,
    },
  };
}

export function toSelectedCharacterView(
  storedCharacter: StoredSelectedCharacter,
  staticSpecs: Map<number, WowSpecialization>
): Character {
  const profile = storedCharacter.profileSummary as BlizzardCharacterProfileSummary;
  const specializations = toSpecializations(storedCharacter.specializationsSummary, staticSpecs);

  return {
    id: storedCharacter.id,
    region: storedCharacter.region,
    realm: storedCharacter.realm,
    name: storedCharacter.name,
    level: profile.level,
    classId: profile.character_class.id,
    raceId: profile.race.id,
    portraitUrl: getServedCharacterPortraitUrl(
      storedCharacter.id,
      storedCharacter.portraitUrl,
      storedCharacter.portraitBlobName
    ),
    fetchedAt: storedCharacter.fetchedAt,
    specializations,
    activeSpecId: storedCharacter.specializationsSummary?.active_specialization?.id ?? null,
  };
}
