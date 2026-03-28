import { describe, expect, it } from "vitest";
import { ACCOUNT_CHARS_COOLDOWN_MS } from "../lib/cache.js";
import { shouldServeCachedAccountProfile } from "./battlenet-characters.js";
import type { RaiderDocument } from "../types/index.js";

function buildRaider(overrides: Partial<RaiderDocument> = {}): RaiderDocument {
  return {
    id: "raider-1",
    battleNetId: "raider-1",
    selectedCharacterId: null,
    createdAt: "2026-03-28T10:00:00.000Z",
    lastSeenAt: "2026-03-28T10:00:00.000Z",
    characters: [],
    ...overrides,
  };
}

describe("shouldServeCachedAccountProfile", () => {
  it("returns false when no cached account profile summary exists", () => {
    expect(shouldServeCachedAccountProfile(buildRaider())).toBe(false);
  });

  it("returns true when the last refresh is still inside the cooldown window", () => {
    const refreshedAt = new Date(Date.now() - (ACCOUNT_CHARS_COOLDOWN_MS - 1_000)).toISOString();

    expect(
      shouldServeCachedAccountProfile(
        buildRaider({
          accountProfileSummary: { wow_accounts: [{ id: 1, characters: [] }] },
          accountProfileRefreshedAt: refreshedAt,
        })
      )
    ).toBe(true);
  });

  it("returns false when a cached summary exists but has never been marked refreshed", () => {
    expect(
      shouldServeCachedAccountProfile(
        buildRaider({
          accountProfileSummary: { wow_accounts: [{ id: 1, characters: [] }] },
        })
      )
    ).toBe(false);
  });

  it("returns false when the last refresh is outside the cooldown window", () => {
    const refreshedAt = new Date(Date.now() - (ACCOUNT_CHARS_COOLDOWN_MS + 1_000)).toISOString();

    expect(
      shouldServeCachedAccountProfile(
        buildRaider({
          accountProfileSummary: { wow_accounts: [{ id: 1, characters: [] }] },
          accountProfileRefreshedAt: refreshedAt,
        })
      )
    ).toBe(false);
  });
});
