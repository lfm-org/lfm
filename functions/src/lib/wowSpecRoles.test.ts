import { describe, it, expect } from "vitest";
import { WOW_SPEC_ROLES, resolveSpecRole } from "./wowSpecRoles.js";

describe("WOW_SPEC_ROLES", () => {
  it("covers all 40 known specs", () => {
    expect(Object.keys(WOW_SPEC_ROLES).length).toBe(40);
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
