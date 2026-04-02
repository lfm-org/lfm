import type { AttendanceStatus } from "./attendanceConfig";
import { normalizeLocalizedString } from "../../../lib/localizedStrings";

export type { AttendanceStatus } from "./attendanceConfig";
export type RaidRole = "TANK" | "HEALER" | "DPS";

export interface RaidSignup {
  id: string;
  characterId: string;
  characterName: string;
  characterRealm: string;
  characterLevel: number;
  characterClassId: number;
  characterClassName: string;
  characterRaceId: number;
  characterRaceName: string;
  isCurrentUser: boolean;
  desiredAttendance: AttendanceStatus;
  reviewedAttendance: AttendanceStatus;
  specId: number | null;
  specName: string | null;
  role: RaidRole | null;
}

export interface Raid {
  id: string;
  startTime: string;
  signupCloseTime: string;
  description: string;
  modeKey: string;
  visibility: "PUBLIC" | "GUILD";
  instanceId: number;
  instanceName: string;
  creatorBattleNetId: string;
  creatorGuild: string;
  createdAt: string;
  raidCharacters: RaidSignup[];
}

export function normalizeRaidSignup(signup: RaidSignup): RaidSignup {
  return {
    ...signup,
    characterName: normalizeLocalizedString(signup.characterName),
    characterRealm: normalizeLocalizedString(signup.characterRealm),
    characterClassName: normalizeLocalizedString(signup.characterClassName),
    characterRaceName: normalizeLocalizedString(signup.characterRaceName),
    specName: signup.specName === null ? null : normalizeLocalizedString(signup.specName),
  };
}

export function normalizeRaid(raid: Raid): Raid {
  return {
    ...raid,
    instanceName: normalizeLocalizedString(raid.instanceName),
    raidCharacters: raid.raidCharacters.map(normalizeRaidSignup),
  };
}
