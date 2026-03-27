import { IANAZone } from "luxon";
import { mergeRankPermissions } from "../guild-permissions.js";
import type { GuildDocument } from "../../types/index.js";

export interface ParsedGuildSettingsInput {
  timezone: string;
  slogan: string | null;
  rankPermissions: NonNullable<GuildDocument["rankPermissions"]>;
}

export function parseGuildSettingsInput(
  input: unknown,
  allowedRanks: number[],
  fallbackRankPermissions: GuildDocument["rankPermissions"],
): ParsedGuildSettingsInput {
  const body = (input ?? {}) as { timezone?: unknown; slogan?: unknown; rankPermissions?: unknown };
  const timezone = typeof body.timezone === "string" ? body.timezone.trim() : "";

  if (!timezone || !IANAZone.isValidZone(timezone)) {
    throw new Error("Invalid timezone");
  }

  return {
    timezone,
    slogan: parseSlogan(body.slogan),
    rankPermissions:
      body.rankPermissions === undefined
        ? mergeRankPermissions(allowedRanks, fallbackRankPermissions)
        : mergeRankPermissions(allowedRanks, parseRankPermissions(body.rankPermissions, allowedRanks)),
  };
}

export function applyGuildSettings(
  guildDoc: GuildDocument,
  settings: ParsedGuildSettingsInput,
  initializedAt = new Date().toISOString(),
): GuildDocument {
  guildDoc.setup = {
    ...guildDoc.setup,
    initializedAt: guildDoc.setup?.initializedAt ?? initializedAt,
    timezone: settings.timezone,
  };
  guildDoc.slogan = settings.slogan;
  guildDoc.rankPermissions = settings.rankPermissions;
  return guildDoc;
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

function parseSlogan(value: unknown): string | null {
  if (value === undefined || value === null) {
    return null;
  }

  if (typeof value !== "string") {
    throw new Error("Invalid slogan");
  }

  const slogan = value.trim();
  if (!slogan) {
    return null;
  }

  if (slogan.length > 120) {
    throw new Error("Invalid slogan");
  }

  return slogan;
}
