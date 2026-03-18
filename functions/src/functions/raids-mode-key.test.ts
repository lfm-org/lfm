import { describe, expect, it } from "vitest";
import { buildRaidDocument, parseCreateRaidBody, validateCreateRaidBody } from "./raids-create.js";
import { applyRaidUpdate, parseRaidUpdateBody } from "./raids-update.js";
import type { BattleNetIdentity, RaidDocument, WowInstance } from "../types/index.js";

const identity: BattleNetIdentity = {
  battleNetId: "bn-123",
  guildName: "Sisu",
  guildId: 99,
};

const existingRaid: RaidDocument = {
  id: "raid-1",
  startTime: "2026-03-20T18:00:00.000Z",
  signupCloseTime: "2026-03-20T16:00:00.000Z",
  description: "Progression",
  modeKey: "NORMAL:10",
  visibility: "GUILD",
  creatorGuild: "Sisu",
  creatorGuildId: 99,
  instanceId: 631,
  instanceName: "Icecrown Citadel",
  creatorBattleNetId: "bn-123",
  createdAt: "2026-03-18T12:00:00.000Z",
  raidCharacters: [],
};

const instances: WowInstance[] = [
  {
    id: 631,
    name: "Icecrown Citadel",
    type: "RAID",
    minLevel: 80,
    expansionId: 3,
    modes: [
      {
        type: "NORMAL",
        name: "Normal",
        players: 10,
        isTracked: true,
        modeKey: "NORMAL:10",
      },
      {
        type: "HEROIC",
        name: "Heroic",
        players: 25,
        isTracked: true,
        modeKey: "HEROIC:25",
      },
    ],
  },
  {
    id: 249,
    name: "Onyxia's Lair",
    type: "RAID",
    minLevel: 80,
    expansionId: 3,
    modes: [
      {
        type: "NORMAL",
        name: "Normal",
        players: 25,
        isTracked: true,
        modeKey: "NORMAL:25",
      },
    ],
  },
];

describe("parseCreateRaidBody", () => {
  it("rejects legacy mode input on create", () => {
    expect(() =>
      parseCreateRaidBody({
        startTime: "2026-03-20T18:00:00.000Z",
        mode: "Heroic",
        modeKey: "HEROIC:25",
        visibility: "PUBLIC",
        instanceId: 631,
      })
    ).toThrowError("Legacy mode is not supported");
  });

  it("requires modeKey for raid creation", () => {
    expect(() =>
      parseCreateRaidBody({
        startTime: "2026-03-20T18:00:00.000Z",
        signupCloseTime: "2026-03-20T16:00:00.000Z",
        description: "Progression",
        visibility: "GUILD",
        instanceId: 631,
        instanceName: "Icecrown Citadel",
      })
    ).toThrowError("Missing required fields");
  });

  it("builds a raid document with modeKey as the source of truth", () => {
    const parsedBody = parseCreateRaidBody({
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
      instanceId: 631,
      instanceName: "Icecrown Citadel",
    });
    const body = validateCreateRaidBody(parsedBody, instances);

    const raid = buildRaidDocument(body, identity, "raid-2", "2026-03-18T12:30:00.000Z");

    expect(raid).toMatchObject({
      id: "raid-2",
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
      creatorGuild: "Sisu",
      creatorGuildId: 99,
      creatorBattleNetId: "bn-123",
    });
    expect(raid).not.toHaveProperty("mode");
  });

  it("rejects an invalid modeKey for the selected instance", () => {
    const body = parseCreateRaidBody({
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      modeKey: "NORMAL:25",
      visibility: "PUBLIC",
      instanceId: 631,
      instanceName: "Icecrown Citadel",
    });

    expect(() => validateCreateRaidBody(body, instances)).toThrowError("Invalid modeKey for instance");
  });
});

describe("raid modeKey updates", () => {
  it("rejects legacy mode input on update", () => {
    expect(() =>
      parseRaidUpdateBody({
        mode: "Heroic",
      })
    ).toThrowError("Legacy mode is not supported");
  });

  it("rejects a non-object update body", () => {
    expect(() => parseRaidUpdateBody("not an object")).toThrowError("Invalid request body");
    expect(() => parseRaidUpdateBody([])).toThrowError("Invalid request body");
  });

  it("preserves the existing modeKey when an update omits it", () => {
    const update = parseRaidUpdateBody({
      description: "More wipes",
    });

    const raid = applyRaidUpdate(existingRaid, update, instances);

    expect(raid.modeKey).toBe("NORMAL:10");
    expect(raid.description).toBe("More wipes");
  });

  it("replaces the existing modeKey when the update provides one", () => {
    const update = parseRaidUpdateBody({
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
    });

    const raid = applyRaidUpdate(existingRaid, update, instances);

    expect(raid.modeKey).toBe("HEROIC:25");
    expect(raid.visibility).toBe("PUBLIC");
  });

  it("rejects an invalid final modeKey and instance combination", () => {
    const update = parseRaidUpdateBody({
      instanceId: 249,
    });

    expect(() => applyRaidUpdate(existingRaid, update, instances)).toThrowError("Invalid modeKey for instance");
  });

  it("normalizes an old-shaped raid when a valid modeKey is supplied", () => {
    const legacyRaid = {
      id: "raid-legacy",
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      visibility: "GUILD",
      creatorGuild: "Sisu",
      creatorGuildId: 99,
      instanceId: 631,
      instanceName: "Icecrown Citadel",
      creatorBattleNetId: "bn-123",
      createdAt: "2026-03-18T12:00:00.000Z",
      raidCharacters: [],
      mode: "Heroic",
    } as unknown as RaidDocument;

    const update = parseRaidUpdateBody({
      modeKey: "HEROIC:25",
    });

    const raid = applyRaidUpdate(legacyRaid, update, instances);

    expect(raid.modeKey).toBe("HEROIC:25");
    expect(raid).not.toHaveProperty("mode");
  });

  it("rejects a pre-migration raid without a valid modeKey when update does not provide one", () => {
    const legacyRaid = {
      id: "raid-legacy",
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      visibility: "GUILD",
      creatorGuild: "Sisu",
      creatorGuildId: 99,
      instanceId: 631,
      instanceName: "Icecrown Citadel",
      creatorBattleNetId: "bn-123",
      createdAt: "2026-03-18T12:00:00.000Z",
      raidCharacters: [],
      mode: "Heroic",
    } as unknown as RaidDocument;

    const update = parseRaidUpdateBody({
      description: "Still progressing",
    });

    expect(() => applyRaidUpdate(legacyRaid, update, instances)).toThrowError("Invalid modeKey for instance");
  });
});
