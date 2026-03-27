import type { GuildHomeResponse } from "./guildHome";

export type GuildRankPermission =
  NonNullable<GuildHomeResponse["settings"]>["rankPermissions"][number];

export interface GuildSettingsDraft {
  timezone: string;
  rankPermissions: GuildRankPermission[];
}

export function createGuildSettingsDraft(data: GuildHomeResponse | null): GuildSettingsDraft {
  return {
    timezone: data?.setup.timezone ?? "Europe/Helsinki",
    rankPermissions: data?.settings?.rankPermissions ?? [],
  };
}

export function updateGuildRankPermission(
  current: GuildRankPermission[],
  rank: number,
  field: "canCreateGuildRaids" | "canSignupGuildRaids",
  checked: boolean,
): GuildRankPermission[] {
  return current.map((permission) =>
    permission.rank === rank ? { ...permission, [field]: checked } : permission,
  );
}

export function toGuildSettingsPayload(draft: GuildSettingsDraft) {
  return {
    timezone: draft.timezone,
    rankPermissions: draft.rankPermissions,
  };
}
