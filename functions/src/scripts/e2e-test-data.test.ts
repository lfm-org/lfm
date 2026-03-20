import { describe, expect, it } from "vitest";
import { getModePlayers as getInstanceModePlayers, toModeKey } from "../lib/wow-instance-modes.js";
import { TEST_MODE_IDENTITY, TEST_MODE_NEEDS_CHARACTER_IDENTITY } from "../lib/test-mode.js";
import {
  assertLocalSeedEnvironment,
  buildReferenceDataWrites,
  buildSeedData,
  resolveE2eScenario,
} from "./e2e-test-data.js";
import type { WowClass, WowInstance, WowRace, WowSpecialization } from "../types/index.js";

const classes: WowClass[] = [
  { id: 1, name: "Warrior" },
  { id: 2, name: "Paladin" },
];

const races: WowRace[] = [
  { id: 1, faction: "ALLIANCE", name: "Human" },
  { id: 2, faction: "HORDE", name: "Orc" },
];

const specializations: WowSpecialization[] = [
  { id: 73, name: "Protection", classId: 1, role: "TANK" },
  { id: 72, name: "Fury", classId: 1, role: "DPS" },
];

const instances: WowInstance[] = [
  {
    id: 63,
    name: "Deadmines",
    type: "DUNGEON",
    minLevel: 35,
    expansionId: 68,
    modes: [
      { mode: { type: "NORMAL", name: "Normal" }, players: 5, is_tracked: true },
      { mode: { type: "HEROIC", name: "Heroic" }, players: 5, is_tracked: true },
    ],
  },
  {
    id: 631,
    name: "Icecrown Citadel",
    type: "RAID",
    minLevel: 80,
    expansionId: 3,
    modes: [
      { mode: { type: "NORMAL", name: "Normal" }, players: 10, is_tracked: true },
      { mode: { type: "NORMAL", name: "Normal" }, players: 25, is_tracked: true },
      { mode: { type: "HEROIC", name: "Heroic" }, players: 10, is_tracked: true },
      { mode: { type: "HEROIC", name: "Heroic" }, players: 25, is_tracked: true },
    ],
  },
  {
    id: 249,
    name: "Onyxia's Lair",
    type: "RAID",
    minLevel: 80,
    expansionId: 3,
    modes: [
      { mode: { type: "NORMAL", name: "Normal" }, players: 25, is_tracked: true },
    ],
  },
  {
    id: 741,
    name: "Molten Core",
    type: "RAID",
    minLevel: 60,
    expansionId: 68,
    modes: [
      { mode: { type: "NORMAL", name: "Normal" }, players: 40, is_tracked: true },
    ],
  },
];

function getRaidModePlayers(raid: { instanceId: number; modeKey: string }): number {
  const instance = instances.find((entry) => entry.id === raid.instanceId);
  if (!instance) return 0;
  return getModePlayersFromInstance(instance, raid.modeKey);
}

function getModePlayersFromInstance(instance: WowInstance, modeKey: string): number {
  const mode = instance.modes.find((entry) => toModeKey(entry) === modeKey);
  return mode ? getInstanceModePlayers(mode) : 0;
}

describe("buildReferenceDataWrites", () => {
  it("produces deterministic data and meta blob writes for the cached WoW reference data", () => {
    const writes = buildReferenceDataWrites(
      { classes, races, specializations, instances },
      "2026-03-18T12:00:00.000Z"
    );

    expect(writes.map((write) => write.blobName).sort()).toEqual([
      "classes-meta.json",
      "classes.json",
      "instances-meta.json",
      "instances.json",
      "races-meta.json",
      "races.json",
      "specializations-meta.json",
      "specializations.json",
    ]);
    expect(writes.find((write) => write.blobName === "instances-meta.json")?.data).toEqual({
      lastSuccessTime: "2026-03-18T12:00:00.000Z",
      lastFailureTime: null,
      lastFailureReason: null,
    });
  });

  it("omits instance blobs for the instances-missing harness scenario", () => {
    const writes = buildReferenceDataWrites(
      { classes, races, specializations, instances },
      "2026-03-18T12:00:00.000Z",
      resolveE2eScenario("instances-missing")
    );

    expect(writes.map((write) => write.blobName)).not.toContain("instances.json");
    expect(writes.map((write) => write.blobName)).not.toContain("instances-meta.json");
  });
});

describe("resolveE2eScenario", () => {
  it("normalizes unknown and missing values to the default harness scenario", () => {
    expect(resolveE2eScenario(undefined)).toBe("default");
    expect(resolveE2eScenario(null)).toBe("default");
    expect(resolveE2eScenario("unknown")).toBe("default");
  });

  it("accepts the named harness scenarios", () => {
    expect(resolveE2eScenario("raids-empty")).toBe("raids-empty");
    expect(resolveE2eScenario("raids-error")).toBe("raids-error");
    expect(resolveE2eScenario("characters-empty")).toBe("characters-empty");
    expect(resolveE2eScenario("instances-missing")).toBe("instances-missing");
  });
});

describe("assertLocalSeedEnvironment", () => {
  it("refuses to run outside the intended local test environment", () => {
    expect(() =>
      assertLocalSeedEnvironment({
        TEST_MODE: "false",
        COSMOS_ENDPOINT: "http://localhost:8081",
      })
    ).toThrowError("seed-test-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");

    expect(() =>
      assertLocalSeedEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "https://localhost:8081",
      })
    ).toThrowError("seed-test-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");

    expect(() =>
      assertLocalSeedEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://example.test:8081",
      })
    ).toThrowError("seed-test-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");

    expect(() =>
      assertLocalSeedEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://localhost:8081",
      })
    ).not.toThrow();

    expect(() =>
      assertLocalSeedEnvironment({
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://cosmosdb:8081",
      })
    ).not.toThrow();
  });
});

describe("buildSeedData", () => {
  it("fails fast when a raid definition references an unknown creator", () => {
    expect(() =>
      buildSeedData({
        now: "2026-03-18T12:00:00.000Z",
        region: "eu",
        instances,
        raidDefinitions: [
          {
            id: "raid-invalid-creator",
            instanceId: 63,
            modeKey: "NORMAL:5",
            visibility: "PUBLIC",
            creatorBattleNetId: "missing-raider",
            description: "Broken fixture",
            startHoursFromNow: 24,
            signupCloseHoursFromNow: 18,
            signupCount: 0,
            pool: "guild",
          },
        ],
      })
    ).toThrowError("Missing creator missing-raider for raid seed raid-invalid-creator");
  });

  it("fails fast when a raid definition requests more signups than its pool can satisfy", () => {
    expect(() =>
      buildSeedData({
        now: "2026-03-18T12:00:00.000Z",
        region: "eu",
        instances,
        raidDefinitions: [
          {
            id: "raid-invalid-signup-count",
            instanceId: 631,
            modeKey: "HEROIC:25",
            visibility: "GUILD",
            creatorBattleNetId: "outsider-raider-01",
            description: "Broken outsider roster",
            startHoursFromNow: 36,
            signupCloseHoursFromNow: 30,
            signupCount: 99,
            pool: "outsider",
          },
        ],
      })
    ).toThrowError("Not enough outsider raiders to seed 25 signups for raid-invalid-signup-count");
  });

  it("builds a guild-scale deterministic dataset with named scenarios and valid mode selections", () => {
    const seed = buildSeedData({
      now: "2026-03-18T12:00:00.000Z",
      region: "eu",
      instances,
    });

    expect(seed.raiders.length).toBeGreaterThanOrEqual(40);
    expect(seed.raids.length).toBeGreaterThanOrEqual(40);

    const testRaider = seed.raiders.find((raider) => raider.battleNetId === TEST_MODE_IDENTITY.battleNetId);
    expect(testRaider).toMatchObject({
      battleNetId: TEST_MODE_IDENTITY.battleNetId,
      guildId: TEST_MODE_IDENTITY.guildId,
      guildName: TEST_MODE_IDENTITY.guildName,
    });
    expect(testRaider?.selectedCharacterId).toBeTruthy();
    expect(testRaider?.characters.length).toBeGreaterThanOrEqual(2);

    const needsCharacterRaider = seed.raiders.find(
      (raider) => raider.battleNetId === TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId
    );
    expect(needsCharacterRaider).toMatchObject({
      battleNetId: TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId,
      guildId: TEST_MODE_NEEDS_CHARACTER_IDENTITY.guildId,
      guildName: TEST_MODE_NEEDS_CHARACTER_IDENTITY.guildName,
      selectedCharacterId: null,
    });
    expect(needsCharacterRaider?.characters.length).toBeGreaterThanOrEqual(2);

    expect(seed.raids.some((raid) => raid.id === "raid-public-signup-target-icc25")).toBe(true);
    expect(seed.raids.some((raid) => raid.id === "raid-public-existing-signup-onyxia25")).toBe(true);
    expect(seed.raids.some((raid) => raid.id === "raid-outsider-guild-hidden")).toBe(true);

    expect(
      seed.raids.every((raid) =>
        instances.some(
          (instance) =>
            instance.id === raid.instanceId &&
            instance.modes.some((mode) => toModeKey(mode) === raid.modeKey)
        )
      )
    ).toBe(true);

    expect(
      seed.raids.every((raid) => raid.raidCharacters.length <= getRaidModePlayers(raid))
    ).toBe(true);
    expect(
      seed.raids.every(
        (raid) => new Set(raid.raidCharacters.map((character) => character.raiderBattleNetId)).size === raid.raidCharacters.length
      )
    ).toBe(true);
    expect(
      seed.raids.every(
        (raid) => new Set(raid.raidCharacters.map((character) => character.id)).size === raid.raidCharacters.length
      )
    ).toBe(true);

    const denseRaid = seed.raids.find((raid) => raid.id === "raid-guild-dense-molten-core");
    expect(denseRaid?.raidCharacters.length).toBeGreaterThanOrEqual(30);

    const fivePlayerRaid = seed.raids.find((raid) => raid.id === "raid-public-empty-deadmines");
    expect(fivePlayerRaid?.raidCharacters.length).toBeLessThanOrEqual(5);

    const existingSignupRaid = seed.raids.find((raid) => raid.id === "raid-public-existing-signup-onyxia25");
    expect(existingSignupRaid?.raidCharacters.slice(0, 4).map((signup) => signup.desiredAttendance)).toEqual([
      "IN",
      "IN",
      "IN",
      "BENCH",
    ]);
  });

  it("builds an empty raids dataset for the raids-empty scenario", () => {
    const seed = buildSeedData({
      now: "2026-03-18T12:00:00.000Z",
      region: "eu",
      instances,
      scenario: "raids-empty",
    });

    expect(seed.raiders.length).toBeGreaterThan(0);
    expect(seed.raids).toEqual([]);
  });

  it("removes cached account characters for the characters-empty scenario", () => {
    const seed = buildSeedData({
      now: "2026-03-18T12:00:00.000Z",
      region: "eu",
      instances,
      scenario: "characters-empty",
    });

    const testRaider = seed.raiders.find((raider) => raider.battleNetId === TEST_MODE_IDENTITY.battleNetId);
    expect(testRaider?.accountCharacters).toEqual([]);
    expect(testRaider?.selectedCharacterId).toBeNull();
  });
});
