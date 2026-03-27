import { describe, expect, it, vi } from "vitest";
import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";
import { resolveAdminGuild, saveCurrentGuildSettings } from "./service.js";

function createRoster(): BlizzardGuildRosterResponse {
  return {
    guild: {
      id: 12345,
      name: "Test Guild",
      realm: {
        id: 559,
        slug: "test-realm",
        name: { en_US: "Test Realm" },
      },
      faction: { type: "ALLIANCE", name: "Alliance" },
    },
    members: [
      {
        character: {
          id: 201,
          name: "Highlord",
          realm: { id: 559, slug: "test-realm", name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 2 },
          playable_race: { id: 3 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 0,
      },
    ],
  };
}

function createGuildDoc(overrides: Partial<GuildDocument> = {}): GuildDocument {
  const profileSummary: BlizzardGuildProfileResponse = {
    id: 12345,
    name: "Test Guild",
    realm: {
      id: 559,
      slug: "test-realm",
      name: { en_US: "Test Realm" },
    },
    faction: { type: "ALLIANCE", name: "Alliance" },
    member_count: 1,
    achievement_points: 10,
  };

  return {
    id: "12345",
    guildId: 12345,
    realmSlug: "test-realm",
    blizzardProfileRaw: profileSummary,
    blizzardProfileFetchedAt: "2026-03-25T10:00:00.000Z",
    blizzardRosterRaw: createRoster(),
    blizzardRosterFetchedAt: "2026-03-25T10:00:00.000Z",
    setup: {
      initializedAt: "2026-03-20T10:00:00.000Z",
      timezone: "Europe/Helsinki",
    },
    slogan: "Old slogan",
    rankPermissions: [
      { rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true },
    ],
    ...overrides,
  };
}

function createRaider(): RaiderDocument {
  return {
    id: "bnet-1",
    battleNetId: "bnet-1",
    selectedCharacterId: "eu-test-realm-highlord",
    createdAt: "2026-03-25T10:00:00.000Z",
    lastSeenAt: "2026-03-25T10:00:00.000Z",
    characters: [
      {
        id: "eu-test-realm-highlord",
        region: "eu",
        realm: "test-realm",
        name: "Highlord",
        fetchedAt: "2026-03-25T10:00:00.000Z",
        profileSummary: {
          name: "Highlord",
          level: 80,
          realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
          character_class: { id: 2, name: "Paladin" },
          race: { id: 1, name: "Human" },
          guild: { id: 12345, name: "Test Guild" },
        },
      },
    ],
  };
}

describe("saveCurrentGuildSettings", () => {
  it("returns an updated guild view after persisting settings", async () => {
    const guildDoc = createGuildDoc();
    const readGuildDocument = vi.fn().mockResolvedValue(guildDoc);
    const readRaider = vi.fn().mockResolvedValue(createRaider());
    const replaceGuildDocument = vi.fn(async (doc: GuildDocument) => doc);

    const result = await saveCurrentGuildSettings({
      guildDocId: "12345",
      battleNetId: "bnet-1",
      accessToken: "token",
      settings: {
        timezone: "America/New_York",
        slogan: "Victory or Lunch",
        rankPermissions: [
          { rank: 0, canCreateGuildRaids: false, canSignupGuildRaids: true },
        ],
      },
      readGuildDocument,
      readRaider,
      replaceGuildDocument,
    });

    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(readRaider).toHaveBeenCalledWith("bnet-1");
    expect(replaceGuildDocument).toHaveBeenCalledWith(expect.objectContaining({
      slogan: "Victory or Lunch",
      setup: {
        initializedAt: "2026-03-20T10:00:00.000Z",
        timezone: "America/New_York",
      },
      rankPermissions: [
        { rank: 0, canCreateGuildRaids: false, canSignupGuildRaids: true },
      ],
    }));
    expect(result.guild).toMatchObject({
      id: 12345,
      name: "Test Guild",
      slogan: "Victory or Lunch",
    });
    expect(result.setup).toMatchObject({
      isInitialized: true,
      timezone: "America/New_York",
    });
  });
});

describe("resolveAdminGuild", () => {
  it("returns the explicit guild id and guild name", async () => {
    const readGuildDocument = vi.fn().mockResolvedValue(createGuildDoc());
    const listRaiders = vi.fn();
    const upsertGuildDocument = vi.fn();

    const result = await resolveAdminGuild({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument,
      listRaiders,
      upsertGuildDocument,
    });

    expect(result).toEqual({
      guildId: "12345",
      guildName: "Test Guild",
    });
    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(listRaiders).not.toHaveBeenCalled();
    expect(upsertGuildDocument).not.toHaveBeenCalled();
  });
});
