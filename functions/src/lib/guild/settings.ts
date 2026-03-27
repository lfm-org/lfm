import { IANAZone } from "luxon";
import { mergeRankPermissions } from "../guild-permissions.js";
import type { GuildDocument } from "../../types/index.js";

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
    rankPermissions:
      body.rankPermissions === undefined
        ? mergeRankPermissions(allowedRanks, fallbackRankPermissions)
        : mergeRankPermissions(allowedRanks, parseRankPermissions(body.rankPermissions, allowedRanks)),
  };
}

function parseRankPermissions(
  providedRankPermissions: unknown,
  allowedRanks: number[],
): NonNullable<GuildDocument["rankPermissions"]> {
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

  return parsed;
}
