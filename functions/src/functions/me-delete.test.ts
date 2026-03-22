import { describe, it, expect } from "vitest";
import { scrubRaidDocument } from "./me-delete.js";
import type { RaidDocument } from "../types/index.js";

const baseRaid: RaidDocument = {
  id: "raid-1",
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
  raidCharacters: [],
};

describe("scrubRaidDocument", () => {
  it("returns modified=false and unchanged raid when user has no involvement", () => {
    const raid = { ...baseRaid };
    const result = scrubRaidDocument(raid, "user-hash");
    expect(result.modified).toBe(false);
    expect(result.raid).toBe(raid);
  });

  it("removes signup entries belonging to the user", () => {
    const raid: RaidDocument = {
      ...baseRaid,
      raidCharacters: [
        {
          id: "rc-1",
          characterId: "char-1",
          characterName: "Aelrin",
          characterRealm: "test-realm",
          characterLevel: 80,
          characterClassId: 2,
          characterClassName: "Paladin",
          characterRaceId: 11,
          characterRaceName: "Draenei",
          raiderBattleNetId: "user-hash",
          desiredAttendance: "IN",
          reviewedAttendance: "IN",
          specId: null,
          specName: null,
          role: null,
        },
        {
          id: "rc-2",
          characterId: "char-2",
          characterName: "Brakka",
          characterRealm: "test-realm",
          characterLevel: 80,
          characterClassId: 1,
          characterClassName: "Warrior",
          characterRaceId: 2,
          characterRaceName: "Orc",
          raiderBattleNetId: "other-hash",
          desiredAttendance: "IN",
          reviewedAttendance: "IN",
          specId: null,
          specName: null,
          role: null,
        },
      ],
    };

    const result = scrubRaidDocument(raid, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.raid.raidCharacters).toHaveLength(1);
    expect(result.raid.raidCharacters[0].raiderBattleNetId).toBe("other-hash");
  });

  it("nulls out creatorBattleNetId when user is the creator", () => {
    const raid: RaidDocument = {
      ...baseRaid,
      creatorBattleNetId: "user-hash",
    };

    const result = scrubRaidDocument(raid, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.raid.creatorBattleNetId).toBeNull();
  });

  it("handles a raid where user is both creator and has a signup", () => {
    const raid: RaidDocument = {
      ...baseRaid,
      creatorBattleNetId: "user-hash",
      raidCharacters: [
        {
          id: "rc-1",
          characterId: "char-1",
          characterName: "Aelrin",
          characterRealm: "test-realm",
          characterLevel: 80,
          characterClassId: 2,
          characterClassName: "Paladin",
          characterRaceId: 11,
          characterRaceName: "Draenei",
          raiderBattleNetId: "user-hash",
          desiredAttendance: "IN",
          reviewedAttendance: "IN",
          specId: null,
          specName: null,
          role: null,
        },
      ],
    };

    const result = scrubRaidDocument(raid, "user-hash");
    expect(result.modified).toBe(true);
    expect(result.raid.creatorBattleNetId).toBeNull();
    expect(result.raid.raidCharacters).toHaveLength(0);
  });
});
