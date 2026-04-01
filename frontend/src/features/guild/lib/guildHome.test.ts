import { describe, expect, it } from "vitest";
import type { GuildHomeResponse } from "./guildHome";
import { normalizeGuildHomeResponse } from "./guildHome";

function createResponse(crestUrl: string | null): GuildHomeResponse {
  return {
    guild: {
      id: 75956957,
      name: "Sisu",
      slogan: null,
      realmSlug: "tarren-mill",
      realmName: "Tarren Mill",
      factionName: "Horde",
      memberCount: 42,
      achievementPoints: 12345,
      syncedMemberCount: 40,
      rankCount: 8,
      crestUrl,
    },
    setup: {
      isInitialized: true,
      requiresSetup: false,
      rankDataFresh: true,
      rankDataFetchedAt: "2026-03-28T00:00:00.000Z",
      timezone: "Europe/Helsinki",
      locale: "fi",
    },
    settings: null,
    editor: {
      canEdit: false,
      mode: "member",
      overrideAvailable: false,
    },
    memberPermissions: {
      matchedRank: 3,
      canCreateGuildRaids: false,
      canSignupGuildRaids: true,
      canDeleteGuildRaids: false,
      rankDataFresh: true,
    },
    adminOverride: null,
  };
}

describe("normalizeGuildHomeResponse", () => {
  it("resolves relative crest URLs against an absolute API base URL", () => {
    const result = normalizeGuildHomeResponse(
      createResponse("/api/guild/75956957/crest"),
      "https://lfm-api.dinosauruskeksi.com/api",
    );

    expect(result.guild?.crestUrl).toBe("https://lfm-api.dinosauruskeksi.com/api/guild/75956957/crest");
  });

  it("preserves relative crest URLs when the API base URL is same-origin relative", () => {
    const result = normalizeGuildHomeResponse(createResponse("/api/guild/75956957/crest"), "/api");

    expect(result.guild?.crestUrl).toBe("/api/guild/75956957/crest");
  });

  it("leaves absolute crest URLs unchanged", () => {
    const result = normalizeGuildHomeResponse(
      createResponse("https://lfmstore.blob.core.windows.net/guild-crests/75956957/crest.svg"),
      "https://lfm-api.dinosauruskeksi.com/api",
    );

    expect(result.guild?.crestUrl).toBe("https://lfmstore.blob.core.windows.net/guild-crests/75956957/crest.svg");
  });
});
