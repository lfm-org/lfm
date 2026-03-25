import { IANAZone } from "luxon";
import type { GuildDocument } from "../../types/index.js";

type GuildRankPermission = NonNullable<GuildDocument["rankPermissions"]>[number];

export function parseGuildSettingsInput(
  input: unknown,
  allowedRanks: number[],
  fallbackRankPermissions: GuildDocument["rankPermissions"],
): { timezone: string; rankPermissions: NonNullable<GuildDocument["rankPermissions"]> } {
  const body = (input ?? {}) as { timezone?: unknown; rankPermissions?: unknown };
  const timezone = typeof body.timezone === "string" ? body.timezone.trim() : "";

  if (!timezone || !IANAZone.isValidZone(timezone)) {
    throw new Error("Invalid timezone");
  }

  return {
    timezone,
    rankPermissions: mergeRankPermissionsByAllowedRanks(
      allowedRanks,
      body.rankPermissions,
      fallbackRankPermissions,
    ),
  };
}

function mergeRankPermissionsByAllowedRanks(
  allowedRanks: number[],
  providedRankPermissions: unknown,
  fallbackRankPermissions: GuildDocument["rankPermissions"],
): NonNullable<GuildDocument["rankPermissions"]> {
  const fallbackByRank = new Map((fallbackRankPermissions ?? []).map((permission) => [permission.rank, permission]));

  if (providedRankPermissions === undefined) {
    return [...new Set(allowedRanks)].sort((left, right) => left - right).map((rank) => {
      const fallback = fallbackByRank.get(rank);
      return fallback ?? {
        rank,
        canCreateGuildRaids: false,
        canSignupGuildRaids: false,
      };
    });
  }

  if (!Array.isArray(providedRankPermissions)) {
    throw new Error("Invalid rank permissions");
  }

  const parsed = providedRankPermissions.map((entry) => {
    if (typeof entry !== "object" || entry === null) {
      throw new Error("Invalid rank permissions");
    }

    const candidate = entry as Record<string, unknown>;
    if (
      typeof candidate.rank !== "number" ||
      typeof candidate.canCreateGuildRaids !== "boolean" ||
      typeof candidate.canSignupGuildRaids !== "boolean"
    ) {
      throw new Error("Invalid rank permissions");
    }

    if (!allowedRanks.includes(candidate.rank)) {
      throw new Error("Unknown guild rank");
    }

    return {
      rank: candidate.rank,
      canCreateGuildRaids: candidate.canCreateGuildRaids,
      canSignupGuildRaids: candidate.canSignupGuildRaids,
    };
  });

  const parsedByRank = new Map(parsed.map((permission) => [permission.rank, permission]));
  return [...new Set(allowedRanks)].sort((left, right) => left - right).map((rank) => {
    const permission = parsedByRank.get(rank);
    if (permission) return permission;
    const fallback = fallbackByRank.get(rank);
    return fallback ?? {
      rank,
      canCreateGuildRaids: false,
      canSignupGuildRaids: false,
    };
  });
}
