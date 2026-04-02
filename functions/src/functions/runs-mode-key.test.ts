import { describe, expect, it } from "vitest";
import { buildRunDocument, parseCreateRunBody, validateCreateRunBody } from "./runs-create.js";
import { applyRunUpdate, parseRunUpdateBody } from "./runs-update.js";
import type { BattleNetIdentity, RunDocument, WowInstance } from "../types/index.js";

const identity: BattleNetIdentity = {
  battleNetId: "bn-123",
  guildName: "Sisu",
  guildId: 99,
};

const existingRun: RunDocument = {
  id: "run-1",
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
  runCharacters: [],
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
        mode: {
          type: "NORMAL",
          name: "Normal",
        },
        players: 10,
        is_tracked: true,
      },
      {
        mode: {
          type: "HEROIC",
          name: "Heroic",
        },
        players: 25,
        is_tracked: true,
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
        mode: {
          type: "NORMAL",
          name: "Normal",
        },
        players: 25,
        is_tracked: true,
      },
    ],
  },
];

describe("parseCreateRunBody", () => {
  it("rejects legacy mode input on create", () => {
    expect(() =>
      parseCreateRunBody({
        startTime: "2026-03-20T18:00:00.000Z",
        mode: "Heroic",
        modeKey: "HEROIC:25",
        visibility: "PUBLIC",
        instanceId: 631,
      })
    ).toThrowError("Unrecognized key");
  });

  it("requires modeKey for run creation", () => {
    expect(() =>
      parseCreateRunBody({
        startTime: "2026-03-20T18:00:00.000Z",
        signupCloseTime: "2026-03-20T16:00:00.000Z",
        description: "Progression",
        visibility: "GUILD",
        instanceId: 631,
        instanceName: "Icecrown Citadel",
      })
    ).toThrowError(); // Zod reports specific missing field
  });

  it("builds a run document with modeKey as the source of truth", () => {
    const parsedBody = parseCreateRunBody({
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
      instanceId: 631,
      instanceName: "Icecrown Citadel",
    });
    const body = validateCreateRunBody(parsedBody, instances);

    const run = buildRunDocument(body, identity, "run-2", "2026-03-18T12:30:00.000Z");

    expect(run).toMatchObject({
      id: "run-2",
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
      creatorGuild: "Sisu",
      creatorGuildId: 99,
      creatorBattleNetId: "bn-123",
    });
    expect(run).not.toHaveProperty("mode");
  });

  it("rejects an invalid modeKey for the selected instance", () => {
    const body = parseCreateRunBody({
      startTime: "2026-03-20T18:00:00.000Z",
      signupCloseTime: "2026-03-20T16:00:00.000Z",
      description: "Progression",
      modeKey: "NORMAL:25",
      visibility: "PUBLIC",
      instanceId: 631,
      instanceName: "Icecrown Citadel",
    });

    expect(() => validateCreateRunBody(body, instances)).toThrowError("Invalid modeKey for instance");
  });
});

describe("run modeKey updates", () => {
  it("rejects legacy mode input on update", () => {
    expect(() =>
      parseRunUpdateBody({
        mode: "Heroic",
      })
    ).toThrowError("Legacy mode is not supported");
  });

  it("rejects a non-object update body", () => {
    expect(() => parseRunUpdateBody("not an object")).toThrowError("Invalid request body");
    expect(() => parseRunUpdateBody([])).toThrowError("Invalid request body");
  });

  it("preserves the existing modeKey when an update omits it", () => {
    const update = parseRunUpdateBody({
      description: "More wipes",
    });

    const run = applyRunUpdate(existingRun, update, instances);

    expect(run.modeKey).toBe("NORMAL:10");
    expect(run.description).toBe("More wipes");
  });

  it("replaces the existing modeKey when the update provides one", () => {
    const update = parseRunUpdateBody({
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
    });

    const run = applyRunUpdate(existingRun, update, instances);

    expect(run.modeKey).toBe("HEROIC:25");
    expect(run.visibility).toBe("PUBLIC");
  });

  it("rejects an invalid final modeKey and instance combination", () => {
    const update = parseRunUpdateBody({
      instanceId: 249,
    });

    expect(() => applyRunUpdate(existingRun, update, instances)).toThrowError("Invalid modeKey for instance");
  });

  it("normalizes an old-shaped run when a valid modeKey is supplied", () => {
    const legacyRun = {
      id: "run-legacy",
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
      runCharacters: [],
      mode: "Heroic",
    } as unknown as RunDocument;

    const update = parseRunUpdateBody({
      modeKey: "HEROIC:25",
    });

    const run = applyRunUpdate(legacyRun, update, instances);

    expect(run.modeKey).toBe("HEROIC:25");
    expect(run).not.toHaveProperty("mode");
  });

  it("rejects a pre-migration run without a valid modeKey when update does not provide one", () => {
    const legacyRun = {
      id: "run-legacy",
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
      runCharacters: [],
      mode: "Heroic",
    } as unknown as RunDocument;

    const update = parseRunUpdateBody({
      description: "Still progressing",
    });

    expect(() => applyRunUpdate(legacyRun, update, instances)).toThrowError("Invalid modeKey for instance");
  });
});
