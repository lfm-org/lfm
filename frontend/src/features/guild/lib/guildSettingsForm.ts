import type { GuildHomeResponse } from "./guildHome";

export type GuildRankPermission =
  NonNullable<GuildHomeResponse["settings"]>["rankPermissions"][number];

export interface GuildSettingsDraft {
  timezone: string;
  locale: string;
  slogan: string;
  rankPermissions: GuildRankPermission[];
}

export function createGuildSettingsDraft(data: GuildHomeResponse | null): GuildSettingsDraft {
  return {
    timezone: data?.setup.timezone ?? "Europe/Helsinki",
    locale: data?.setup.locale ?? "fi",
    slogan: data?.guild?.slogan ?? "",
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
    locale: draft.locale,
    slogan: draft.slogan,
    rankPermissions: draft.rankPermissions,
  };
}
