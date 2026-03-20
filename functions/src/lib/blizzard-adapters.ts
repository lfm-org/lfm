import { resolveSpecRole } from "./wowSpecRoles.js";
import type {
  BlizzardAccountGuildsSummary,
  BlizzardAccountProfileSummary,
  BlizzardCharacterMediaSummary,
  BlizzardCharacterProfileSummary,
  BlizzardCharacterSpecializationsSummary,
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
  region: string
): AccountCharacter[] {
  return (summary.wow_accounts ?? []).flatMap((account) =>
    (account.characters ?? []).map((character) => ({
      name: character.name,
      realm: character.realm.slug,
      realmName: localizeName(character.realm.name) || character.realm.slug,
      level: character.level,
      region,
    }))
  );
}

export function toBattleNetIdentity(
  battleNetId: string,
  guildSummary?: BlizzardAccountGuildsSummary | null
): BattleNetIdentity {
  const guild = guildSummary?.guilds?.[0]?.guild;
  return {
    battleNetId,
    guildId: guild?.id ?? null,
    guildName: guild?.name ?? null,
  };
}

function findAvatarUrl(mediaSummary?: BlizzardCharacterMediaSummary | null): string {
  return mediaSummary?.assets?.find((asset) => asset.key === "avatar")?.value ?? "";
}

function toSpecializations(
  specializationsSummary: BlizzardCharacterSpecializationsSummary | null | undefined,
  staticSpecs: Map<number, WowSpecialization>
): NonNullable<Character["specializations"]> {
  return (specializationsSummary?.specializations ?? []).map((entry) => ({
    id: entry.specialization.id,
    name: entry.specialization.name,
    role: staticSpecs.get(entry.specialization.id)?.role ?? resolveSpecRole(entry.specialization.id),
  }));
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
    portraitUrl: findAvatarUrl(storedCharacter.mediaSummary),
    fetchedAt: storedCharacter.fetchedAt,
    specializations,
    activeSpecId: storedCharacter.specializationsSummary?.active_specialization?.id ?? null,
  };
}
