import { describe, expect, it } from "vitest";
import { normalizeRunSignup, type RunSignup } from "./runTypes";

function buildSignup(overrides: Partial<RunSignup> = {}): RunSignup {
  return {
    id: "signup-1",
    characterId: "eu-test-realm-aelrin",
    characterName: "Aelrin",
    characterRealm: "test-realm",
    characterLevel: 80,
    characterClassId: 2,
    characterClassName: "Paladin",
    characterRaceId: 11,
    characterRaceName: "Draenei",
    isCurrentUser: false,
    desiredAttendance: "IN",
    reviewedAttendance: "IN",
    specId: 65,
    specName: "Holy",
    role: "HEALER",
    ...overrides,
  };
}

describe("normalizeRunSignup", () => {
  it("passes through a fully-populated signup with string fields unchanged", () => {
    const result = normalizeRunSignup(buildSignup());
    expect(result.characterName).toBe("Aelrin");
    expect(result.characterRealm).toBe("test-realm");
    expect(result.specName).toBe("Holy");
    expect(result.specId).toBe(65);
  });

  it("preserves null specName as null", () => {
    const result = normalizeRunSignup(buildSignup({ specName: null }));
    expect(result.specName).toBeNull();
  });

  it("preserves non-string fields (numeric, boolean) unchanged", () => {
    const result = normalizeRunSignup(buildSignup({ characterLevel: 80, isCurrentUser: true }));
    expect(result.characterLevel).toBe(80);
    expect(result.isCurrentUser).toBe(true);
  });
});
