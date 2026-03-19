import { describe, expect, it } from "vitest";
import { canReuseCachedCharacter } from "./raider-character.js";
import { TEST_MODE_ACCESS_TOKEN } from "../lib/test-mode.js";
import type { Character } from "../types/index.js";

function buildCharacter(fetchedAt: string): Character {
  return {
    id: "eu-test-realm-aelrin",
    region: "eu",
    realm: "test-realm",
    name: "Aelrin",
    level: 80,
    classId: 2,
    raceId: 11,
    portraitUrl: "https://example.test/aelrin.jpg",
    fetchedAt,
    specializations: [
      { id: 65, name: "Holy", role: "HEALER" },
      { id: 66, name: "Protection", role: "TANK" },
    ],
    activeSpecId: 65,
  };
}

describe("canReuseCachedCharacter", () => {
  it("reuses seeded local-test-mode characters even when their timestamp is older than the normal TTL", () => {
    const staleCharacter = buildCharacter("2026-03-18T12:00:00.000Z");

    expect(
      canReuseCachedCharacter(staleCharacter, TEST_MODE_ACCESS_TOKEN, {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://localhost:8081",
      })
    ).toBe(true);
  });

  it("still requires freshness outside local test mode", () => {
    const staleCharacter = buildCharacter("2026-03-18T12:00:00.000Z");

    expect(canReuseCachedCharacter(staleCharacter, "real-token")).toBe(false);
  });
});
