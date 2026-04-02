import type { BlizzardGuildRosterResponse } from "../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";
import { resolveMatchedGuildRanks } from "./guild-member-match.js";

export const GUILD_ROSTER_TTL_MS = 60 * 60 * 1000;

export interface GuildRankPermission {
  rank: number;
  canCreateGuildRuns: boolean;
  canSignupGuildRuns: boolean;
  canDeleteGuildRuns: boolean;
}

export interface EffectiveGuildPermissions {
  matchedRank: number | null;
  canCreateGuildRuns: boolean;
  canSignupGuildRuns: boolean;
  canDeleteGuildRuns: boolean;
  rankDataFresh: boolean;
}

export function getGuildRanksFromRoster(roster?: BlizzardGuildRosterResponse): number[] {
  if (!roster) return [];
  return [...new Set(roster.members.map((member) => member.rank))].sort((left, right) => left - right);
}

export function buildDefaultRankPermissions(ranks: number[]): GuildRankPermission[] {
  return [...new Set(ranks)].sort((left, right) => left - right).map((rank) => ({
    rank,
    canCreateGuildRuns: rank === 0,
    canSignupGuildRuns: true,
    canDeleteGuildRuns: rank === 0,
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
          canCreateGuildRuns: storedPermission.canCreateGuildRuns,
          canSignupGuildRuns: storedPermission.canSignupGuildRuns,
          canDeleteGuildRuns: storedPermission.canDeleteGuildRuns ?? permission.canDeleteGuildRuns,
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
  const matchedRanks = resolveMatchedGuildRanks(raider, roster);
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
      canCreateGuildRuns: false,
      canSignupGuildRuns: false,
      canDeleteGuildRuns: false,
      rankDataFresh: false,
    };
  }

  const matchedRank = resolveMatchedRank(raider, guildDoc.blizzardRosterRaw);
  if (matchedRank === null) {
    return {
      matchedRank: null,
      canCreateGuildRuns: false,
      canSignupGuildRuns: false,
      canDeleteGuildRuns: false,
      rankDataFresh: true,
    };
  }

  const rankPermissions = getResolvedRankPermissions(guildDoc);
  const permission = rankPermissions.find((entry) => entry.rank === matchedRank);

  return {
    matchedRank,
    canCreateGuildRuns: permission?.canCreateGuildRuns ?? false,
    canSignupGuildRuns: permission?.canSignupGuildRuns ?? false,
    canDeleteGuildRuns: permission?.canDeleteGuildRuns ?? false,
    rankDataFresh: true,
  };
}
