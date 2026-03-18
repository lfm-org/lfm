import type { AttendanceStatus } from "./attendanceConfig";

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
  raiderBattleNetId: string;
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
