import { describe, it, expect } from "vitest";
import { WOW_SPEC_ROLES, resolveSpecRole } from "./wowSpecRoles.js";

const EXPECTED_SPEC_IDS = [
  62, 63, 64,
  65, 66, 70,
  71, 72, 73,
  102, 103, 104, 105,
  250, 251, 252,
  253, 254, 255,
  256, 257, 258,
  259, 260, 261,
  262, 263, 264,
  265, 266, 267,
  268, 269, 270,
  577, 581,
  1467, 1468, 1473,
];

describe("WOW_SPEC_ROLES", () => {
  it("covers all 39 known specs", () => {
    expect(Object.keys(WOW_SPEC_ROLES).map(Number).sort((a, b) => a - b)).toEqual(EXPECTED_SPEC_IDS);
  });

  it("has at least one TANK spec", () => {
    expect(Object.values(WOW_SPEC_ROLES)).toContain("TANK");
  });

  it("has at least one HEALER spec", () => {
    expect(Object.values(WOW_SPEC_ROLES)).toContain("HEALER");
  });

  it("has at least one DPS spec", () => {
    expect(Object.values(WOW_SPEC_ROLES)).toContain("DPS");
  });

  it("maps Warrior Protection (73) to TANK", () => {
    expect(WOW_SPEC_ROLES[73]).toBe("TANK");
  });

  it("maps Priest Holy (257) to HEALER", () => {
    expect(WOW_SPEC_ROLES[257]).toBe("HEALER");
  });

  it("maps Mage Arcane (62) to DPS", () => {
    expect(WOW_SPEC_ROLES[62]).toBe("DPS");
  });
});

describe("resolveSpecRole", () => {
  it("returns the role for a known specId", () => {
    expect(resolveSpecRole(73)).toBe("TANK");
  });

  it("returns DPS as default for unknown specId", () => {
    expect(resolveSpecRole(99999)).toBe("DPS");
  });

  it("returns DPS as default for null", () => {
    expect(resolveSpecRole(null)).toBe("DPS");
  });
});
