import type { BlizzardGuildRosterResponse } from "../types/blizzard.js";
import type { GuildDocument, RaiderDocument, StoredSelectedCharacter } from "../types/index.js";

export const GUILD_ROSTER_TTL_MS = 60 * 60 * 1000;

export interface GuildRankPermission {
  rank: number;
  canCreateGuildRaids: boolean;
  canSignupGuildRaids: boolean;
}

export interface EffectiveGuildPermissions {
  matchedRank: number | null;
  canCreateGuildRaids: boolean;
  canSignupGuildRaids: boolean;
  rankDataFresh: boolean;
}

function normalizeRosterKey(realmSlug: string, name: string): string {
  return `${realmSlug.toLowerCase()}:${name.toLowerCase()}`;
}

export function getGuildRanksFromRoster(roster?: BlizzardGuildRosterResponse): number[] {
  if (!roster) return [];
  return [...new Set(roster.members.map((member) => member.rank))].sort((left, right) => left - right);
}

export function buildDefaultRankPermissions(ranks: number[]): GuildRankPermission[] {
  return [...new Set(ranks)].sort((left, right) => left - right).map((rank) => ({
    rank,
    canCreateGuildRaids: rank === 0,
    canSignupGuildRaids: true,
  }));
}

export function mergeRankPermissions(
  ranks: number[],
  stored?: GuildDocument["rankPermissions"]
): GuildRankPermission[] {
  const storedByRank = new Map((stored ?? []).map((permission) => [permission.rank, permission]));
  return buildDefaultRankPermissions(ranks).map((permission) => {
    const storedPermission = storedByRank.get(permission.rank);
    return storedPermission
      ? {
          rank: permission.rank,
          canCreateGuildRaids: storedPermission.canCreateGuildRaids,
          canSignupGuildRaids: storedPermission.canSignupGuildRaids,
        }
      : permission;
  });
}

export function isGuildRosterFresh(guildDoc?: GuildDocument | null, now = Date.now()): boolean {
  if (!guildDoc?.blizzardRosterFetchedAt) return false;
  return now - new Date(guildDoc.blizzardRosterFetchedAt).getTime() < GUILD_ROSTER_TTL_MS;
}

export function getResolvedRankPermissions(guildDoc?: GuildDocument | null): GuildRankPermission[] {
  return mergeRankPermissions(getGuildRanksFromRoster(guildDoc?.blizzardRosterRaw), guildDoc?.rankPermissions);
}

function resolveMatchedRank(
  raider: RaiderDocument | undefined,
  roster: BlizzardGuildRosterResponse | undefined
): number | null {
  if (!raider || !roster) return null;

  const rosterRanks = new Map<string, number>();
  for (const member of roster.members) {
    rosterRanks.set(normalizeRosterKey(member.character.realm.slug, member.character.name), member.rank);
  }

  const matchedRanks = raider.characters.flatMap((character: StoredSelectedCharacter) => {
    const rank = rosterRanks.get(normalizeRosterKey(character.realm, character.name));
    return rank === undefined ? [] : [rank];
  });

  return matchedRanks.length > 0 ? Math.min(...matchedRanks) : null;
}

export function getEffectiveGuildPermissions(
  guildDoc: GuildDocument | null | undefined,
  raider: RaiderDocument | undefined,
  now = Date.now()
): EffectiveGuildPermissions {
  if (!guildDoc || !isGuildRosterFresh(guildDoc, now)) {
    return {
      matchedRank: null,
      canCreateGuildRaids: false,
      canSignupGuildRaids: false,
      rankDataFresh: false,
    };
  }

  const matchedRank = resolveMatchedRank(raider, guildDoc.blizzardRosterRaw);
  if (matchedRank === null) {
    return {
      matchedRank: null,
      canCreateGuildRaids: false,
      canSignupGuildRaids: false,
      rankDataFresh: true,
    };
  }

  const rankPermissions = getResolvedRankPermissions(guildDoc);
  const permission = rankPermissions.find((entry) => entry.rank === matchedRank);

  return {
    matchedRank,
    canCreateGuildRaids: permission?.canCreateGuildRaids ?? false,
    canSignupGuildRaids: permission?.canSignupGuildRaids ?? false,
    rankDataFresh: true,
  };
}
