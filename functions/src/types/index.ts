import type {
  BlizzardAccountGuildsSummary,
  BlizzardAccountProfileSummary,
  BlizzardCharacterMediaSummary,
  BlizzardCharacterProfileSummary,
  BlizzardCharacterSpecializationsSummary,
  BlizzardGuildProfileResponse,
  BlizzardGuildRosterResponse,
  BlizzardMediaSummary,
} from "./blizzard.js";

// Raider document (Cosmos container: raiders, partition key: /battleNetId)
export interface AccountCharacter {
  name: string;
  realm: string;       // realm slug
  realmName: string;   // display name, resolved at fetch time
  level: number;
  region: string;
  classId?: number;
  className?: string;
  portraitUrl?: string;
  activeSpecId?: number | null;
  specName?: string | null;
}

export interface Character {
  id: string;
  region: string;
  realm: string;
  name: string;
  level: number;
  classId: number;
  raceId: number;
  portraitUrl: string;
  fetchedAt?: string;
  specializations?: Array<{
    id: number;
    name: string;
    role: "TANK" | "HEALER" | "DPS";
  }>;
  activeSpecId?: number | null;
}

export interface StoredSelectedCharacter {
  id: string;
  region: string;
  realm: string;
  name: string;
  portraitBlobName?: string;
  portraitUrl?: string;
  fetchedAt?: string;
  profileSummary: BlizzardCharacterProfileSummary;
  mediaSummary?: BlizzardCharacterMediaSummary | null;
  specializationsSummary?: BlizzardCharacterSpecializationsSummary | null;
}

export interface RaiderDocument {
  id: string;
  battleNetId: string;
  selectedCharacterId: string | null;
  createdAt: string;
  lastSeenAt: string;
  accountProfileSummary?: BlizzardAccountProfileSummary;
  accountProfileFetchedAt?: string;
  accountProfileRefreshedAt?: string;
  accountGuildsSummary?: BlizzardAccountGuildsSummary;
  characters: StoredSelectedCharacter[];
  portraitCache?: Record<string, string>;
  locale?: string;
  ttl?: number;
  blizzardEtags?: {
    accountProfile?: string;
    characterProfile?: Record<string, string>;
    guildRoster?: string;
    media?: Record<string, string>;
  };
}

// Guild document (Cosmos container: guilds, partition key: /id)
export interface GuildDocument {
  id: string;                              // guildId as string
  guildId: number;
  blizzardEtags?: {
    accountProfile?: string;
    characterProfile?: Record<string, string>;
    guildRoster?: string;
    media?: Record<string, string>;
  };
  realmSlug: string;
  slogan?: string | null;
  profileSummary?: BlizzardGuildProfileResponse;
  profileFetchedAt?: string;
  blizzardProfileRaw?: BlizzardGuildProfileResponse;
  blizzardProfileFetchedAt?: string;
  blizzardRosterRaw?: BlizzardGuildRosterResponse;
  blizzardRosterFetchedAt?: string;
  blizzardCrestEmblemMediaRaw?: BlizzardMediaSummary;
  blizzardCrestBorderMediaRaw?: BlizzardMediaSummary;
  blizzardCrestMediaFetchedAt?: string;
  crestEmblemUrl?: string;
  crestBorderUrl?: string;
  crestBlobName?: string;
  crestEmblemBlobName?: string;
  crestBorderBlobName?: string;
  crestUrl?: string;
  rankPermissions?: Array<{
    rank: number;
    canCreateGuildRuns: boolean;
    canSignupGuildRuns: boolean;
    canDeleteGuildRuns?: boolean;
  }>;
  lastOverrideBy?: string;
  lastOverrideAt?: string;
  setup?: {
    initializedAt?: string;
    timezone?: string;
    locale?: string;
  };
}

// Run document (Cosmos container: runs, partition key: /id)
export type AttendanceStatus = "IN" | "OUT" | "BENCH" | "LATE" | "AWAY";
export type RunVisibility = "PUBLIC" | "GUILD";

export interface RunCharacter {
  id: string;
  characterId: string;
  characterName: string;
  characterRealm: string;
  characterLevel: number;
  characterClassId: number;
  characterClassName: string;
  characterRaceId: number;
  characterRaceName: string;
  raiderBattleNetId: string;
  desiredAttendance: AttendanceStatus;
  reviewedAttendance: AttendanceStatus;
  specId: number | null;
  specName: string | null;
  role: "TANK" | "HEALER" | "DPS" | null;
}

export interface RunDocument {
  id: string;
  startTime: string;
  signupCloseTime: string;
  description: string;
  modeKey: string;
  visibility: RunVisibility;
  creatorGuild: string;
  creatorGuildId: number | null;
  instanceId: number;
  instanceName: string;
  creatorBattleNetId: string | null;
  createdAt: string;
  ttl: number;
  runCharacters: RunCharacter[];
}

// Blob metadata schema
export interface EntitySyncMeta {
  lastSuccessTime: string | null;
  lastFailureTime: string | null;
  lastFailureReason: string | null;
}

// WoW reference data types
export interface WowClass {
  id: number;
  name: string;
}

export interface WowRace {
  id: number;
  faction: string;
  name: string;
}

export interface WowInstanceMode {
  mode: {
    type: string;
    name: string;
  };
  players?: number;
  is_tracked?: boolean;
}

export interface WowInstance {
  id: number;
  name: string;
  type: string;
  minLevel: number;
  expansionId: number;
  modes: WowInstanceMode[];
  mediaUrl?: string;
}

export interface WowSpecialization {
  id: number;
  name: string;
  classId: number;
  role: "TANK" | "HEALER" | "DPS";
  iconUrl?: string;
}

// Auth identity (returned by requireAuth)
export interface BattleNetIdentity {
  battleNetId: string;
  guildName: string | null;
  guildId: number | null;
}

// Battle.net callback result (returned by BattlenetService.handleCallback)
export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  redirect: string | null;
  guildName: string | null;
  selectedCharacterId: string | null;
}
