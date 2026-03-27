import { describe, expect, it } from "vitest";
import { isGuildVisibilityPromotion, parseRaidUpdateBody } from "./raids-update.js";

describe("parseRaidUpdateBody", () => {
  it("rejects legacy mode field", () => {
    expect(() => parseRaidUpdateBody({ mode: "normal" })).toThrow("Legacy mode");
  });

  it("rejects invalid visibility", () => {
    expect(() => parseRaidUpdateBody({ visibility: "PRIVATE" })).toThrow("Invalid visibility");
  });

  it("parses valid partial update", () => {
    const result = parseRaidUpdateBody({ description: "Updated" });
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
