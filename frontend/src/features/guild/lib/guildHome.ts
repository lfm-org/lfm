export interface GuildHomeResponse {
  guild: {
    id: number;
    name: string;
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
