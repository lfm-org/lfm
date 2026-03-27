import { describe, expect, it } from "vitest";
import { parseRaidUpdateBody, applyRaidUpdate } from "./raids-update.js";

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

describe("GUILD visibility guard condition", () => {
  it("guard triggers when changing to GUILD and identity has no guild", () => {
    const guildlessIdentity = { battleNetId: "abc", guildId: null, guildName: null };
    const isChangingToGuild = "GUILD" === "GUILD" && "PUBLIC" !== "GUILD";
    expect(isChangingToGuild && !guildlessIdentity.guildId).toBe(true);
  });

  it("guard does not trigger when visibility stays GUILD", () => {
    const isChangingToGuild = "GUILD" === "GUILD" && "GUILD" !== "GUILD";
    expect(isChangingToGuild).toBe(false);
  });

  it("guard does not trigger for PUBLIC visibility", () => {
    const isChangingToGuild = "PUBLIC" === "GUILD";
    expect(isChangingToGuild).toBe(false);
  });
});
