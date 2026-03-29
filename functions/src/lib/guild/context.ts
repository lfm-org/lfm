import type { BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { RaiderDocument } from "../../types/index.js";
import { resolveMatchedGuildRanks } from "../guild-member-match.js";

export type GuildEditorResolution = {
  canEdit: boolean;
  mode: "member" | "guild-master";
  matchedRank: number | null;
};

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
  const matchedRanks = resolveMatchedGuildRanks(raider, roster);
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
