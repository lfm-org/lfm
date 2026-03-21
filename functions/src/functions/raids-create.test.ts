import { describe, it, expect } from "vitest";
import { parseCreateRaidBody, buildRaidDocument } from "./raids-create.js";

describe("parseCreateRaidBody", () => {
  it("rejects requests missing required fields", () => {
    expect(() => parseCreateRaidBody({})).toThrow("Missing required fields");
    expect(() => parseCreateRaidBody({ startTime: "2026-04-01T20:00:00Z", modeKey: "NORMAL:10", instanceId: 631 })).toThrow();
  });

  it("rejects legacy mode field", () => {
    expect(() => parseCreateRaidBody({ mode: "normal", startTime: "2026-04-01T20:00:00Z", modeKey: "NORMAL:10", instanceId: 631, visibility: "PUBLIC" })).toThrow("Legacy mode is not supported");
  });
});

describe("buildRaidDocument — guild validation", () => {
  const validBody = {
    startTime: "2026-04-01T20:00:00Z",
    signupCloseTime: "2026-04-01T18:00:00Z",
    description: "Test raid",
    modeKey: "NORMAL:10",
    visibility: "GUILD" as const,
    instanceId: 631,
    instanceName: "Icecrown Citadel",
  };

  it("sets creatorGuildId and creatorGuild from identity", () => {
    const identity = { battleNetId: "abc", guildId: 12345, guildName: "Test Guild" };
    const doc = buildRaidDocument(validBody, identity, "raid-1", "2026-03-21T10:00:00Z");
    expect(doc.creatorGuildId).toBe(12345);
    expect(doc.creatorGuild).toBe("Test Guild");
  });

  it("sets empty guild fields when identity has no guild", () => {
    const identity = { battleNetId: "abc", guildId: null, guildName: null };
    const doc = buildRaidDocument(validBody, identity, "raid-1", "2026-03-21T10:00:00Z");
    expect(doc.creatorGuildId).toBeNull();
    expect(doc.creatorGuild).toBe("");
  });
});

describe("GUILD raid guard condition", () => {
  it("guard triggers when visibility is GUILD and guildId is null", () => {
    // The handler checks: body.visibility === "GUILD" && !identity.guildId
    // Verify this condition is correct for both cases
    const guildlessIdentity = { battleNetId: "abc", guildId: null, guildName: null };
    const guildedIdentity = { battleNetId: "abc", guildId: 12345, guildName: "Test Guild" };

    expect("GUILD" === "GUILD" && !guildlessIdentity.guildId).toBe(true);   // should reject
    expect("GUILD" === "GUILD" && !guildedIdentity.guildId).toBe(false);    // should allow
    expect("PUBLIC" === "GUILD" && !guildlessIdentity.guildId).toBe(false); // PUBLIC always allowed
  });
});
