import { describe, expect, it } from "vitest";
import { isGuildVisibilityPromotion, parseRunUpdateBody, applyRunUpdate, type UpdateRunBody } from "./runs-update.js";
import { isEditingClosed, getLockedFields } from "../lib/run-editability.js";
import type { RunDocument, WowInstance } from "../types/index.js";

describe("parseRunUpdateBody", () => {
  it("rejects legacy mode field", () => {
    expect(() => parseRunUpdateBody({ mode: "normal" })).toThrow("Legacy mode");
  });

  it("rejects invalid visibility", () => {
    expect(() => parseRunUpdateBody({ visibility: "PRIVATE" })).toThrow("Invalid visibility");
  });

  it("parses valid partial update", () => {
    const result = parseRunUpdateBody({ description: "Updated" });
    expect(result).toEqual({ description: "Updated" });
  });
});

describe("isGuildVisibilityPromotion", () => {
  it("returns true when changing from PUBLIC to GUILD", () => {
    expect(isGuildVisibilityPromotion("GUILD", "PUBLIC")).toBe(true);
  });

  it("returns false when visibility stays GUILD", () => {
    expect(isGuildVisibilityPromotion("GUILD", "GUILD")).toBe(false);
  });

  it("returns false when visibility is PUBLIC", () => {
    expect(isGuildVisibilityPromotion("PUBLIC", "PUBLIC")).toBe(false);
  });

  it("returns false when visibility is undefined (no change requested)", () => {
    expect(isGuildVisibilityPromotion(undefined, "PUBLIC")).toBe(false);
    expect(isGuildVisibilityPromotion(undefined, "GUILD")).toBe(false);
  });

  it("returns false when changing from GUILD to PUBLIC", () => {
    expect(isGuildVisibilityPromotion("PUBLIC", "GUILD")).toBe(false);
  });
});

const STUB_INSTANCES: WowInstance[] = [
  {
    id: 631,
    name: "Icecrown Citadel",
    type: "RAID",
    expansionId: 3,
    minLevel: 80,
    modes: [
      { mode: { type: "NORMAL", name: "Normal" }, players: 10 },
      { mode: { type: "NORMAL", name: "Normal" }, players: 25 },
      { mode: { type: "HEROIC", name: "Heroic" }, players: 10 },
      { mode: { type: "HEROIC", name: "Heroic" }, players: 25 },
    ],
  },
];

function stubRun(overrides: Partial<RunDocument> = {}): RunDocument {
  return {
    id: "run-1",
    startTime: "2026-04-05T19:00:00Z",
    signupCloseTime: "2026-04-05T17:00:00Z",
    description: "Test run",
    modeKey: "NORMAL:10",
    visibility: "GUILD",
    creatorGuild: "Test Guild",
    creatorGuildId: 12345,
    instanceId: 631,
    instanceName: "Icecrown Citadel",
    creatorBattleNetId: "creator-bnet",
    createdAt: "2026-04-01T10:00:00Z",
    ttl: 86400,
    runCharacters: [],
    ...overrides,
  };
}

describe("editability enforcement with applyRunUpdate", () => {
  it("allows all fields when no signups", () => {
    const run = stubRun({ runCharacters: [] });
    const body: UpdateRunBody = { startTime: "2026-04-06T19:00:00Z", instanceId: 631 };
    const locked = getLockedFields(run.runCharacters.length);
    expect(locked.size).toBe(0);
    const result = applyRunUpdate(run, body, STUB_INSTANCES);
    expect(result.startTime).toBe("2026-04-06T19:00:00Z");
  });

  it("flags locked fields when signups exist", () => {
    const run = stubRun({ runCharacters: [{ id: "s1" }] as never[] });
    const locked = getLockedFields(run.runCharacters.length);
    expect(locked.has("startTime")).toBe(true);
    expect(locked.has("instanceId")).toBe(true);
  });

  it("editing is closed when signupCloseTime passed", () => {
    expect(isEditingClosed("2026-04-01T10:00:00Z", "2026-04-10T19:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });

  it("editing is closed when startTime passed", () => {
    expect(isEditingClosed("", "2026-04-01T10:00:00Z", "2026-04-01T12:00:00Z")).toBe(true);
  });
});
