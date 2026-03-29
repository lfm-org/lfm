import type { BlizzardGuildRosterResponse } from "../types/blizzard.js";
import type { RaiderDocument, StoredSelectedCharacter } from "../types/index.js";

function normalizeRosterKey(realmSlug: string, name: string): string {
  return `${realmSlug.toLowerCase()}:${name.toLowerCase()}`;
}

function getStoredCharacterRosterKey(character: StoredSelectedCharacter): string | null {
  const realmSlug = character.profileSummary?.realm?.slug ?? character.realm;
  const name = character.profileSummary?.name ?? character.name;
  if (!realmSlug || !name) return null;
  return normalizeRosterKey(realmSlug, name);
}

function getStoredCharacterProfileId(character: StoredSelectedCharacter): number | null {
  const candidate = (character.profileSummary as { id?: unknown } | undefined)?.id;
  return typeof candidate === "number" ? candidate : null;
}

export function resolveMatchedGuildRanks(
  raider: RaiderDocument | undefined,
  roster: BlizzardGuildRosterResponse | undefined,
): number[] {
  if (!raider || !roster) return [];

  const rosterRanksById = new Map<number, number>();
  const rosterRanksByKey = new Map<string, number>();
  for (const member of roster.members) {
    if (typeof member.character.id === "number") {
      rosterRanksById.set(member.character.id, member.rank);
    }
    rosterRanksByKey.set(normalizeRosterKey(member.character.realm.slug, member.character.name), member.rank);
  }

  return raider.characters.flatMap((character) => {
    const profileId = getStoredCharacterProfileId(character);
    if (profileId !== null) {
      const rank = rosterRanksById.get(profileId);
      if (rank !== undefined) return [rank];
    }

    const rosterKey = getStoredCharacterRosterKey(character);
    if (!rosterKey) return [];

    const rank = rosterRanksByKey.get(rosterKey);
    return rank === undefined ? [] : [rank];
  });
}
