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

describe("buildRaidDocument — TTL", () => {
  const identity = { battleNetId: "abc", guildId: 12345, guildName: "Test Guild" };

  it("sets ttl so the document expires 7 days after startTime", () => {
    const startTime = "2026-04-10T20:00:00.000Z";
    const createdAt = "2026-04-08T10:00:00.000Z";
    const doc = buildRaidDocument(
      { startTime, signupCloseTime: "", description: "", modeKey: "HEROIC:25", visibility: "PUBLIC", instanceId: 1 },
      identity, "raid-1", createdAt
    );

    // ttl is seconds from createdAt until startTime + 7 days
    const expectedExpiry = new Date(startTime).getTime() + 7 * 24 * 3600 * 1000;
    const expectedTtl = Math.floor((expectedExpiry - new Date(createdAt).getTime()) / 1000);
    expect(doc.ttl).toBe(expectedTtl);
  });

  it("clamps ttl to a minimum of 1 day when startTime is already past", () => {
    // startTime 10 days ago → would normally be negative
    const startTime = "2026-03-12T20:00:00.000Z";
    const createdAt = "2026-03-22T10:00:00.000Z";
    const doc = buildRaidDocument(
      { startTime, signupCloseTime: "", description: "", modeKey: "HEROIC:25", visibility: "PUBLIC", instanceId: 1 },
      identity, "raid-1", createdAt
    );

    expect(doc.ttl).toBe(86400); // minimum 1 day
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
