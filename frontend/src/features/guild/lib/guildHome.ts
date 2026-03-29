import { resolveApiAssetUrl } from "../../../lib/api";

export interface GuildHomeResponse {
  guild: {
    id: number;
    name: string;
    slogan: string | null;
    realmSlug: string;
    realmName: string;
    factionName: string | null;
    memberCount: number | null;
    achievementPoints: number | null;
    syncedMemberCount: number | null;
    rankCount: number | null;
    crestUrl: string | null;
  } | null;
  setup: {
    isInitialized: boolean;
    requiresSetup: boolean;
    rankDataFresh: boolean;
    rankDataFetchedAt: string | null;
    timezone: string;
    locale: string;
  };
  settings: {
    rankPermissions: Array<{
      rank: number;
      canCreateGuildRaids: boolean;
      canSignupGuildRaids: boolean;
    }>;
  } | null;
  editor: {
    canEdit: boolean;
    mode: "member" | "guild-master" | "site-admin";
    overrideAvailable: boolean;
  };
  memberPermissions: {
    matchedRank: number | null;
    canCreateGuildRaids: boolean;
    canSignupGuildRaids: boolean;
    rankDataFresh: boolean;
  };
  adminOverride: {
    lastOverrideBy: string | null;
    lastOverrideAt: string | null;
  } | null;
}

export function normalizeGuildHomeResponse(
  data: GuildHomeResponse,
  apiBaseUrl?: string
): GuildHomeResponse {
  if (!data.guild?.crestUrl) return data;

  return {
    ...data,
    guild: {
      ...data.guild,
      crestUrl: resolveApiAssetUrl(data.guild.crestUrl, apiBaseUrl),
    },
  };
}
