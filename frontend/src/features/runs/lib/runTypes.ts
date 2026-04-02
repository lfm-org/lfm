import type { AttendanceStatus } from "./attendanceConfig";
import { normalizeLocalizedString } from "../../../lib/localizedStrings";

export type { AttendanceStatus } from "./attendanceConfig";
export type RunRole = "TANK" | "HEALER" | "DPS";

export interface RunSignup {
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
  role: RunRole | null;
}

export interface Run {
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
  runCharacters: RunSignup[];
}

export function normalizeRunSignup(signup: RunSignup): RunSignup {
  return {
    ...signup,
    characterName: normalizeLocalizedString(signup.characterName),
    characterRealm: normalizeLocalizedString(signup.characterRealm),
    characterClassName: normalizeLocalizedString(signup.characterClassName),
    characterRaceName: normalizeLocalizedString(signup.characterRaceName),
    specName: signup.specName === null ? null : normalizeLocalizedString(signup.specName),
  };
}

export function normalizeRun(run: Run): Run {
  return {
    ...run,
    instanceName: normalizeLocalizedString(run.instanceName),
    runCharacters: run.runCharacters.map(normalizeRunSignup),
  };
}
