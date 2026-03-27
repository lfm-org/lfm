import { afterEach, describe, expect, it, vi } from "vitest";
import type { BlizzardGuildProfileResponse, BlizzardGuildRosterResponse } from "../../types/blizzard.js";
import type { GuildDocument, RaiderDocument } from "../../types/index.js";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "./document.js";
import {
  BlizzardGuildRefreshError,
  loadAdminGuildHome,
  loadCurrentGuildHome,
  resolveAdminGuild,
  saveAdminGuildSettings,
  saveCurrentGuildSettings,
} from "./service.js";

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
  const freshTimestamp = new Date().toISOString();
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
    blizzardProfileFetchedAt: freshTimestamp,
    blizzardRosterRaw: createRoster(),
    blizzardRosterFetchedAt: freshTimestamp,
    setup: {
      initializedAt: "2026-03-20T10:00:00.000Z",
      timezone: "Europe/Helsinki",
    },
    slogan: "Old slogan",
    rankPermissions: [{ rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true }],
    ...overrides,
  };
}

function createRaider(name = "Highlord"): RaiderDocument {
  return {
    id: "bnet-1",
    battleNetId: "bnet-1",
    selectedCharacterId: `eu-test-realm-${name.toLowerCase()}`,
    createdAt: "2026-03-25T10:00:00.000Z",
    lastSeenAt: "2026-03-25T10:00:00.000Z",
    characters: [
      {
        id: `eu-test-realm-${name.toLowerCase()}`,
        region: "eu",
        realm: "test-realm",
        name,
        fetchedAt: "2026-03-25T10:00:00.000Z",
        profileSummary: {
          name,
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
    const readRawInput = vi.fn().mockResolvedValue({
      timezone: "America/New_York",
      slogan: "Victory or Lunch",
    });
    const replaceGuildDocument = vi.fn(async (doc: GuildDocument) => doc);

    const result = await saveCurrentGuildSettings({
      guildId: 12345,
      guildName: "Test Guild",
      battleNetId: "bnet-1",
      readRawInput,
      readGuildDocument,
      readRaider,
      replaceGuildDocument,
    });

    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(readRaider).toHaveBeenCalledWith("bnet-1");
    expect(readRawInput).toHaveBeenCalledTimes(1);
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

  it("returns missing_guild before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));

    const result = await saveCurrentGuildSettings({
      guildId: null,
      guildName: null,
      battleNetId: "bnet-1",
      readRawInput,
      readGuildDocument: vi.fn(),
      readRaider: vi.fn(),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "missing_guild" });
    expect(readRawInput).not.toHaveBeenCalled();
  });

  it("returns not_found before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));
    const readGuildDocument = vi.fn().mockResolvedValue(null);

    const result = await saveCurrentGuildSettings({
      guildId: 12345,
      guildName: "Test Guild",
      battleNetId: "bnet-1",
      readRawInput,
      readGuildDocument,
      readRaider: vi.fn(),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "not_found" });
    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(readRawInput).not.toHaveBeenCalled();
  });

  it("returns forbidden before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));

    const result = await saveCurrentGuildSettings({
      guildId: 12345,
      guildName: "Test Guild",
      battleNetId: "bnet-1",
      readRawInput,
      readGuildDocument: vi.fn().mockResolvedValue(createGuildDoc()),
      readRaider: vi.fn().mockResolvedValue(createRaider("Peon")),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "forbidden" });
    expect(readRawInput).not.toHaveBeenCalled();
  });

  it("returns stale before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));

    const result = await saveCurrentGuildSettings({
      guildId: 12345,
      guildName: "Test Guild",
      battleNetId: "bnet-1",
      readRawInput,
      readGuildDocument: vi.fn().mockResolvedValue(createGuildDoc({
        blizzardRosterFetchedAt: "2026-03-25T08:00:00.000Z",
      })),
      readRaider: vi.fn().mockResolvedValue(createRaider()),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "stale" });
    expect(readRawInput).not.toHaveBeenCalled();
  });
});

describe("saveAdminGuildSettings", () => {
  it("bootstraps a missing guild doc before saving admin settings", async () => {
    const guildDoc = createGuildDoc({ lastOverrideBy: undefined, lastOverrideAt: undefined });
    const readGuildDocument = vi.fn().mockImplementation(() => {
      throw new Error("admin save should not read the guild doc before bootstrap");
    });
    const listRaiders = vi.fn().mockResolvedValue([]);
    const readRawInput = vi.fn().mockResolvedValue({
      timezone: "Europe/Helsinki",
      slogan: "Admin override",
    });
    const replaceGuildDocument = vi.fn(async (doc: GuildDocument) => doc);

    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(guildDoc);

    const result = await saveAdminGuildSettings({
      guildDocId: "12345",
      accessToken: "token",
      battleNetId: "admin-bnet",
      readRawInput,
      readGuildDocument,
      listRaiders,
      replaceGuildDocument,
    });

    expect(ensureGuildDocumentForAdmin).toHaveBeenCalledTimes(1);
    expect(readGuildDocument).not.toHaveBeenCalled();
    expect(listRaiders).not.toHaveBeenCalled();
    expect(readRawInput).toHaveBeenCalledTimes(1);
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

  it("returns not_found before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));

    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(null);

    const result = await saveAdminGuildSettings({
      guildDocId: "12345",
      accessToken: "token",
      battleNetId: "admin-bnet",
      readRawInput,
      readGuildDocument: vi.fn(),
      listRaiders: vi.fn(),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "not_found" });
    expect(readRawInput).not.toHaveBeenCalled();
  });

  it("returns stale before attempting to read malformed input", async () => {
    const readRawInput = vi.fn().mockRejectedValue(new Error("bad json"));

    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(createGuildDoc({
      blizzardRosterFetchedAt: "2026-03-25T08:00:00.000Z",
    }));

    const result = await saveAdminGuildSettings({
      guildDocId: "12345",
      accessToken: "token",
      battleNetId: "admin-bnet",
      readRawInput,
      readGuildDocument: vi.fn(),
      listRaiders: vi.fn(),
      replaceGuildDocument: vi.fn(),
    });

    expect(result).toEqual({ kind: "stale" });
    expect(readRawInput).not.toHaveBeenCalled();
  });
});

describe("resolveAdminGuild", () => {
  it("returns explicit guild id and guild name from the bootstrapped guild doc", async () => {
    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(createGuildDoc());

    const result = await resolveAdminGuild({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument: vi.fn(),
      listRaiders: vi.fn(),
      upsertGuildDocument: vi.fn(),
    });

    expect(ensureGuildDocumentForAdmin).toHaveBeenCalledTimes(1);
    expect(result).toEqual({
      guildId: "12345",
      guildName: "Test Guild",
    });
  });
});

describe("loadAdminGuildHome", () => {
  it("returns the admin guild view from the bootstrapped guild doc", async () => {
    vi.mocked(ensureGuildDocumentForAdmin).mockResolvedValue(createGuildDoc({
      lastOverrideBy: "admin-bnet",
      lastOverrideAt: "2026-03-25T12:00:00.000Z",
    }));

    const result = await loadAdminGuildHome({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument: vi.fn(),
      listRaiders: vi.fn(),
      upsertGuildDocument: vi.fn(),
    });

    expect(ensureGuildDocumentForAdmin).toHaveBeenCalledTimes(1);
    expect(result).toMatchObject({
      guild: {
        id: 12345,
        name: "Test Guild",
      },
      editor: {
        canEdit: true,
        mode: "site-admin",
      },
      adminOverride: {
        lastOverrideBy: "admin-bnet",
        lastOverrideAt: "2026-03-25T12:00:00.000Z",
      },
    });
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
