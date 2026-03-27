import { afterEach, describe, expect, it, vi } from "vitest";
import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "./document.js";
import { BlizzardGuildRefreshError, loadCurrentGuildHome, saveAdminGuildSettings, saveCurrentGuildSettings } from "./service.js";

vi.mock("./document.js", () => ({
  ensureGuildDocumentForAdmin: vi.fn(),
  refreshGuildDocument: vi.fn(),
}));

afterEach(() => {
  vi.clearAllMocks();
});

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
    rankPermissions: [{ rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true }],
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
  it("reads its own state, parses raw input, and persists an updated view", async () => {
    const guildDoc = createGuildDoc();
    const readGuildDocument = vi.fn().mockResolvedValue(guildDoc);
    const readRaider = vi.fn().mockResolvedValue(createRaider());
    const replaceGuildDocument = vi.fn(async (doc: GuildDocument) => doc);

    const result = await saveCurrentGuildSettings({
      guildId: 12345,
      guildName: "Test Guild",
      battleNetId: "bnet-1",
      rawInput: {
        timezone: "America/New_York",
        slogan: "Victory or Lunch",
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
    }));
    expect(result.kind).toBe("ok");
    if (result.kind === "ok") {
      expect(result.view.guild).toMatchObject({
        id: 12345,
        slogan: "Victory or Lunch",
      });
      expect(result.view.setup).toMatchObject({
        isInitialized: true,
        timezone: "America/New_York",
      });
    }
  });
});

describe("saveAdminGuildSettings", () => {
  it("bootstraps a missing guild doc before saving admin settings", async () => {
    const guildDoc = createGuildDoc({ lastOverrideBy: undefined, lastOverrideAt: undefined });
    const readGuildDocument = vi.fn().mockImplementation(() => {
      throw new Error("admin save should not read the guild doc before bootstrap");
    });
    const listRaiders = vi.fn().mockResolvedValue([]);
    const replaceGuildDocument = vi.fn(async (doc: GuildDocument) => doc);

    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(guildDoc);

    const result = await saveAdminGuildSettings({
      guildDocId: "12345",
      accessToken: "token",
      battleNetId: "admin-bnet",
      rawInput: {
        timezone: "Europe/Helsinki",
        slogan: "Admin override",
      },
      readGuildDocument,
      listRaiders,
      replaceGuildDocument,
    });

    expect(ensureGuildDocumentForAdmin).toHaveBeenCalledTimes(1);
    expect(readGuildDocument).not.toHaveBeenCalled();
    expect(listRaiders).not.toHaveBeenCalled();
    expect(result.kind).toBe("ok");
    if (result.kind === "ok") {
      expect(result.view.guild).toMatchObject({
        id: 12345,
        slogan: "Admin override",
      });
      expect(result.view.adminOverride).toMatchObject({
        lastOverrideBy: "admin-bnet",
        lastOverrideAt: expect.any(String),
      });
    }
  });
});

describe("loadCurrentGuildHome", () => {
  it("surfaces Blizzard refresh failures with a typed error", async () => {
    vi.mocked(refreshGuildDocument).mockRejectedValue(new Error("blizzard down"));

    await expect(
      loadCurrentGuildHome({
        guildId: 12345,
        guildName: "Test Guild",
        battleNetId: "bnet-1",
        accessToken: "token",
        readGuildDocument: vi.fn().mockResolvedValue(null),
        readRaider: vi.fn().mockResolvedValue(createRaider()),
        upsertGuildDocument: vi.fn(),
        log: vi.fn(),
      }),
    ).rejects.toBeInstanceOf(BlizzardGuildRefreshError);
  });
});
