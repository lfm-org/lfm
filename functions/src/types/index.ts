// Raider document (Cosmos container: raiders, partition key: /battleNetId)
export interface AccountCharacter {
  name: string;
  realm: string;       // realm slug
  realmName: string;   // display name, resolved at fetch time
  level: number;
  region: string;
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

export interface RaiderDocument {
  id: string;
  battleNetId: string;
  guildName: string | null;
  guildId: number | null;
  selectedCharacterId: string | null;
  createdAt: string;
  characters: Character[];
  accountCharacters?: AccountCharacter[];
  accountCharactersFetchedAt?: string;
  accountCharactersRefreshedAt?: string;
}

// Raid document (Cosmos container: raids, partition key: /id)
export type AttendanceStatus = "IN" | "OUT" | "BENCH" | "LATE" | "AWAY";
export type RaidVisibility = "PUBLIC" | "GUILD";

export interface RaidCharacter {
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

export interface RaidDocument {
  id: string;
  startTime: string;
  signupCloseTime: string;
  description: string;
  mode: string;
  visibility: RaidVisibility;
  creatorGuild: string;
  creatorGuildId: number | null;
  instanceId: number;
  instanceName: string;
  creatorBattleNetId: string;
  createdAt: string;
  raidCharacters: RaidCharacter[];
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

export interface WowInstance {
  id: number;
  name: string;
  type: string;
  minLevel: number;
  expansionId: number;
  modes: string[];
}

export interface WowSpecialization {
  id: number;
  name: string;
  classId: number;
  role: "TANK" | "HEALER" | "DPS";
}

// Auth identity (returned by requireAuth)
export interface BattleNetIdentity {
  battleNetId: string;
  guildName: string | null;
  guildId: number | null;
}

// Encrypted cookie payload
export interface TokenPayload {
  accessToken: string;
  issuedAt: number;
  expiresIn: number;
}

// Battle.net callback result (returned by BattlenetService.handleCallback)
export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  redirect: string | null;
  guildName: string | null;
  selectedCharacterId: string | null;
}
