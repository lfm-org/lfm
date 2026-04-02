import { describe, it, expect } from "vitest";
import { scrubRunDocument } from "./raider-cleanup.js";
import type { RunDocument } from "../types/index.js";

const baseRun: RunDocument = {
  id: "run-1",
  startTime: "2026-04-01T20:00:00Z",
  signupCloseTime: "2026-04-01T18:00:00Z",
  description: "",
  modeKey: "NORMAL:10",
  visibility: "PUBLIC",
  creatorGuild: "Test Guild",
  creatorGuildId: 12345,
  instanceId: 1,
  instanceName: "Test Instance",
  creatorBattleNetId: "other-raider-hash",
  createdAt: "2026-03-20T10:00:00Z",
  ttl: 86400,
  runCharacters: [],
};

describe("scrubRunDocument", () => {
  it("returns modified=false and unchanged run when user has no involvement", () => {
    const run = { ...baseRun };
    const result = scrubRunDocument(run, "user-hash");
    expect(result.modified).toBe(false);
    expect(result.run).toBe(run);
  });

  it("removes signup entries belonging to the user", () => {
    const run: RunDocument = {
      ...baseRun,
      runCharacters: [
        {
          id: "rc-1", characterId: "char-1", characterName: "Aelrin",
          characterRealm: "test-realm", characterLevel: 80,
          characterClassId: 2, characterClassName: "Paladin",
          characterRaceId: 11, characterRaceName: "Draenei",
          raiderBattleNetId: "user-hash",
          desiredAttendance: "IN", reviewedAttendance: "IN",
          specId: null, specName: null, role: null,
        },
        {
          id: "rc-2", characterId: "char-2", characterName: "Brakka",
          characterRealm: "test-realm", characterLevel: 80,
          characterClassId: 1, characterClassName: "Warrior",
          characterRaceId: 2, characterRaceName: "Orc",
          raiderBattleNetId: "other-hash",
          desiredAttendance: "IN", reviewedAttendance: "IN",
          specId: null, specName: null, role: null,
        },
      ],
    };
    const result = scrubRunDocument(run, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.run.runCharacters).toHaveLength(1);
    expect(result.run.runCharacters[0].raiderBattleNetId).toBe("other-hash");
  });

  it("nulls out creatorBattleNetId when user is the creator", () => {
    const run: RunDocument = { ...baseRun, creatorBattleNetId: "user-hash" };
    const result = scrubRunDocument(run, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.run.creatorBattleNetId).toBeNull();
  });

  it("handles a run where user is both creator and has a signup", () => {
    const run: RunDocument = {
      ...baseRun,
      creatorBattleNetId: "user-hash",
      runCharacters: [
        {
          id: "rc-1", characterId: "char-1", characterName: "Aelrin",
          characterRealm: "test-realm", characterLevel: 80,
          characterClassId: 2, characterClassName: "Paladin",
          characterRaceId: 11, characterRaceName: "Draenei",
          raiderBattleNetId: "user-hash",
          desiredAttendance: "IN", reviewedAttendance: "IN",
          specId: null, specName: null, role: null,
        },
      ],
    };
    const result = scrubRunDocument(run, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.run.creatorBattleNetId).toBeNull();
    expect(result.run.runCharacters).toHaveLength(0);
  });
});
