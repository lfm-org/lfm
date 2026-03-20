import { describe, expect, it } from "vitest";
import { canReuseCachedCharacter } from "./raider-character.js";
import { TEST_MODE_ACCESS_TOKEN } from "../lib/test-mode.js";
import type { StoredSelectedCharacter } from "../types/index.js";

function buildCharacter(fetchedAt: string): StoredSelectedCharacter {
  return {
    id: "eu-test-realm-aelrin",
    region: "eu",
    realm: "test-realm",
    name: "Aelrin",
    fetchedAt,
    profileSummary: {
      name: "Aelrin",
      level: 80,
      realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
      character_class: { id: 2, name: "Paladin" },
      race: { id: 11, name: "Draenei" },
    },
    mediaSummary: {
      assets: [{ key: "avatar", value: "https://example.test/aelrin.jpg" }],
    },
    specializationsSummary: {
      specializations: [
        { specialization: { id: 65, name: "Holy" } },
        { specialization: { id: 66, name: "Protection" } },
      ],
      active_specialization: { id: 65, name: "Holy" },
    },
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
