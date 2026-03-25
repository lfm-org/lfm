import type { BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { RaiderDocument, StoredSelectedCharacter } from "../../types/index.js";

export type GuildEditorResolution = {
  canEdit: boolean;
  mode: "member" | "guild-master";
  matchedRank: number | null;
};

function normalizeRosterKey(realmSlug: string, name: string): string {
  return `${realmSlug.toLowerCase()}:${name.toLowerCase()}`;
}

export function parseGuildId(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? "";
  return /^\d+$/.test(trimmed) ? trimmed : null;
}

export function toGuildNameSlug(name: string): string {
  return name.toLowerCase().replace(/\s+/g, "-").replace(/[^a-z0-9-]/g, "");
}

export function resolveRealmSlug(raider: RaiderDocument | undefined): string | null {
  if (!raider) return null;
  const selectedChar = raider.characters.find((c) => c.id === raider.selectedCharacterId) ?? raider.characters[0];
  return selectedChar?.realm ?? null;
}

export function resolveGuildEditor(
  raider: RaiderDocument | undefined,
  roster: BlizzardGuildRosterResponse | undefined,
): GuildEditorResolution {
  if (!raider || !roster) {
    return { canEdit: false, mode: "member", matchedRank: null };
  }

  const rosterRanks = new Map<string, number>();
  for (const member of roster.members) {
    rosterRanks.set(normalizeRosterKey(member.character.realm.slug, member.character.name), member.rank);
  }

  const matchedRanks = raider.characters.flatMap((character: StoredSelectedCharacter) => {
    const rank = rosterRanks.get(normalizeRosterKey(character.realm, character.name));
    return rank === undefined ? [] : [rank];
  });

  if (matchedRanks.length === 0) {
    return { canEdit: false, mode: "member", matchedRank: null };
  }

  const bestRank = Math.min(...matchedRanks);
  return {
    canEdit: bestRank === 0,
    mode: bestRank === 0 ? "guild-master" : "member",
    matchedRank: bestRank,
  };
}
