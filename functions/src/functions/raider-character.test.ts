import { describe, expect, it } from "vitest";
import { isCharacterOwnedByAccount, canReuseCachedCharacter } from "./raider-character.js";
import { TEST_MODE_ACCESS_TOKEN } from "../lib/test-mode.js";
import type { StoredSelectedCharacter } from "../types/index.js";
import type { BlizzardAccountProfileSummary } from "../types/blizzard.js";

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

describe("isCharacterOwnedByAccount", () => {
  const summary: BlizzardAccountProfileSummary = {
    wow_accounts: [
      {
        id: 1,
        characters: [
          {
            name: "Aelrin",
            level: 80,
            realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
            playable_class: { id: 2, name: "Paladin" },
            playable_race: { id: 11, name: "Draenei" },
            faction: { type: "ALLIANCE", name: "Alliance" },
            gender: { type: "FEMALE", name: "Female" },
            protected_character: { href: "https://example.test/aelrin" },
          },
        ],
      },
    ],
  };

  it("returns true when the character is in the account profile", () => {
    expect(isCharacterOwnedByAccount("eu-test-realm-aelrin", "eu", summary)).toBe(true);
  });

  it("returns false when the character is not in the account profile", () => {
    expect(isCharacterOwnedByAccount("eu-test-realm-unknownchar", "eu", summary)).toBe(false);
  });

  it("returns true when the account profile is absent (allows onboarding flow)", () => {
    expect(isCharacterOwnedByAccount("eu-test-realm-aelrin", "eu", undefined)).toBe(true);
  });

  it("is case-insensitive: matches when Blizzard returns an all-caps character name", () => {
    const upperCaseSummary: BlizzardAccountProfileSummary = {
      wow_accounts: [
        {
          id: 1,
          characters: [
            {
              name: "AELRIN",
              level: 80,
              realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
              playable_class: { id: 2, name: "Paladin" },
              playable_race: { id: 11, name: "Draenei" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "FEMALE", name: "Female" },
              protected_character: { href: "https://example.test/aelrin" },
            },
          ],
        },
      ],
    };
    expect(isCharacterOwnedByAccount("eu-test-realm-aelrin", "eu", upperCaseSummary)).toBe(true);
  });

  it("returns false when wow_accounts is empty", () => {
    expect(
      isCharacterOwnedByAccount("eu-test-realm-aelrin", "eu", { wow_accounts: [] })
    ).toBe(false);
  });
});
