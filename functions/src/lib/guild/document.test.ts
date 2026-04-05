import { describe, expect, it, vi } from "vitest";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "./document.js";

describe("refreshGuildDocument", () => {
  it("assembles fetched Blizzard payloads and preserves cached guild metadata", async () => {
    const profileSummary = {
      id: 12345,
      name: "Test Guild",
      realm: { slug: "test-realm", name: "Test Realm" },
    } as never;
    const rosterSummary = {
      members: [{ rank: 0, character: { name: "Aelrin", realm: { slug: "test-realm" } } }],
    } as never;
    const crest = {
      crestEmblemUrl: "https://cdn.test/emblem.png",
      crestBorderUrl: "https://cdn.test/border.png",
      emblemEtag: "W/\"emblem-etag\"",
      borderEtag: "W/\"border-etag\"",
    };
    const fetchGuildProfile = vi.fn().mockResolvedValue({ notModified: false, body: profileSummary, etag: "W/\"profile-etag\"" });
    const fetchGuildRoster = vi.fn().mockResolvedValue({ notModified: false, body: rosterSummary, etag: "W/\"roster-etag\"" });
    const syncGuildCrestForDocument = vi.fn().mockResolvedValue(crest);
    const upsertGuildDocument = vi.fn(async (doc) => doc);

    const result = await refreshGuildDocument({
      guildDocId: "12345",
      guildId: 12345,
      guildName: "Test Guild",
      realmSlug: "test-realm",
      accessToken: "token",
      cached: {
        id: "12345",
        guildId: 12345,
        realmSlug: "test-realm",
        slogan: "Keep pushing",
        setup: { timezone: "Europe/Helsinki", initializedAt: "2026-03-20T00:00:00.000Z" },
        rankPermissions: [{ rank: 1, canCreateGuildRuns: true, canSignupGuildRuns: false }],
        lastOverrideAt: "2026-03-21T12:00:00.000Z",
        lastOverrideBy: "admin-123",
      } as never,
      fetchGuildProfile,
      fetchGuildRoster,
      syncGuildCrestForDocument,
      upsertGuildDocument,
    });

    expect(fetchGuildProfile).toHaveBeenCalledWith("test-realm", "test-guild", "token", undefined);
    expect(fetchGuildRoster).toHaveBeenCalledWith("test-realm", "test-guild", "token", undefined);
    expect(syncGuildCrestForDocument).toHaveBeenCalledWith("12345", profileSummary, "token", expect.anything());
    expect(upsertGuildDocument).toHaveBeenCalledTimes(1);
    expect(upsertGuildDocument).toHaveBeenCalledWith(expect.objectContaining({
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      slogan: "Keep pushing",
      profileSummary,
      profileFetchedAt: expect.any(String),
      blizzardProfileRaw: profileSummary,
      blizzardProfileFetchedAt: expect.any(String),
      blizzardRosterRaw: rosterSummary,
      blizzardRosterFetchedAt: expect.any(String),
      crestEmblemUrl: "https://cdn.test/emblem.png",
      crestBorderUrl: "https://cdn.test/border.png",
      rankPermissions: [{ rank: 1, canCreateGuildRuns: true, canSignupGuildRuns: false }],
      setup: { timezone: "Europe/Helsinki", initializedAt: "2026-03-20T00:00:00.000Z" },
      lastOverrideAt: "2026-03-21T12:00:00.000Z",
      lastOverrideBy: "admin-123",
      blizzardEtags: {
        accountProfile: "W/\"profile-etag\"",
        guildRoster: "W/\"roster-etag\"",
        media: { emblem: "W/\"emblem-etag\"", border: "W/\"border-etag\"" },
      },
    }));
    expect(result).toMatchObject({
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      blizzardEtags: {
        accountProfile: "W/\"profile-etag\"",
        guildRoster: "W/\"roster-etag\"",
        media: { emblem: "W/\"emblem-etag\"", border: "W/\"border-etag\"" },
      },
    });
  });

  it("skips re-parsing when Blizzard responds with 304 for profile and roster", async () => {
    const cachedProfile = {
      id: 12345,
      name: "Test Guild",
      realm: { slug: "test-realm", name: "Test Realm" },
    } as never;
    const cachedRoster = { members: [] } as never;
    const cachedEtags = {
      accountProfile: "W/\"old-profile-etag\"",
      guildRoster: "W/\"old-roster-etag\"",
    };
    const fetchGuildProfile = vi.fn().mockResolvedValue({ notModified: true, etag: cachedEtags.accountProfile });
    const fetchGuildRoster = vi.fn().mockResolvedValue({ notModified: true, etag: cachedEtags.guildRoster });
    const syncGuildCrestForDocument = vi.fn().mockResolvedValue(null);
    const upsertGuildDocument = vi.fn(async (doc) => doc);

    const cachedDoc = {
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      blizzardProfileRaw: cachedProfile,
      blizzardProfileFetchedAt: "2026-04-01T10:00:00.000Z",
      blizzardRosterRaw: cachedRoster,
      blizzardRosterFetchedAt: "2026-04-01T10:00:00.000Z",
      blizzardEtags: cachedEtags,
    } as never;

    const result = await refreshGuildDocument({
      guildDocId: "12345",
      guildId: 12345,
      guildName: "Test Guild",
      realmSlug: "test-realm",
      accessToken: "token",
      cached: cachedDoc,
      fetchGuildProfile,
      fetchGuildRoster,
      syncGuildCrestForDocument,
      upsertGuildDocument,
    });

    expect(fetchGuildProfile).toHaveBeenCalledWith("test-realm", "test-guild", "token", cachedEtags.accountProfile);
    expect(fetchGuildRoster).toHaveBeenCalledWith("test-realm", "test-guild", "token", cachedEtags.guildRoster);
    // On 304, blizzardProfileFetchedAt and blizzardRosterFetchedAt preserve the cached timestamps
    expect(upsertGuildDocument).toHaveBeenCalledWith(expect.objectContaining({
      blizzardProfileRaw: cachedProfile,
      blizzardRosterRaw: cachedRoster,
      blizzardProfileFetchedAt: "2026-04-01T10:00:00.000Z",
      blizzardRosterFetchedAt: "2026-04-01T10:00:00.000Z",
      blizzardEtags: cachedEtags,
    }));
    expect(result).toMatchObject({
      blizzardProfileRaw: cachedProfile,
      blizzardRosterRaw: cachedRoster,
    });
  });
});

describe("ensureGuildDocumentForAdmin", () => {
  it("returns an existing guild document without calling bootstrap dependencies", async () => {
    const existing = { id: "12345", guildId: 12345, realmSlug: "test-realm" } as never;
    const readGuildDocument = vi.fn().mockResolvedValue(existing);
    const listRaiders = vi.fn();
    const fetchGuildProfile = vi.fn();
    const fetchGuildRoster = vi.fn();
    const syncGuildCrestForDocument = vi.fn();
    const upsertGuildDocument = vi.fn();

    const result = await ensureGuildDocumentForAdmin({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument,
      listRaiders,
      fetchGuildProfile,
      fetchGuildRoster,
      syncGuildCrestForDocument,
      upsertGuildDocument,
    });

    expect(result).toBe(existing);
    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(listRaiders).not.toHaveBeenCalled();
    expect(fetchGuildProfile).not.toHaveBeenCalled();
    expect(fetchGuildRoster).not.toHaveBeenCalled();
    expect(syncGuildCrestForDocument).not.toHaveBeenCalled();
    expect(upsertGuildDocument).not.toHaveBeenCalled();
  });

  it("returns null when no raider can supply guild context", async () => {
    const readGuildDocument = vi.fn().mockResolvedValue(null);
    const listRaiders = vi.fn().mockResolvedValue([]);
    const fetchGuildProfile = vi.fn();
    const fetchGuildRoster = vi.fn();
    const syncGuildCrestForDocument = vi.fn();
    const upsertGuildDocument = vi.fn();

    const result = await ensureGuildDocumentForAdmin({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument,
      listRaiders,
      fetchGuildProfile,
      fetchGuildRoster,
      syncGuildCrestForDocument,
      upsertGuildDocument,
    });

    expect(result).toBeNull();
    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(listRaiders).toHaveBeenCalledTimes(1);
    expect(fetchGuildProfile).not.toHaveBeenCalled();
    expect(fetchGuildRoster).not.toHaveBeenCalled();
    expect(syncGuildCrestForDocument).not.toHaveBeenCalled();
    expect(upsertGuildDocument).not.toHaveBeenCalled();
  });

  it("bootstraps a guild document from raider guild context", async () => {
    const profileSummary = {
      id: 12345,
      name: "Test Guild",
      realm: { slug: "test-realm", name: "Test Realm" },
    } as never;
    const rosterSummary = { members: [] } as never;
    const crest = {
      crestEmblemUrl: "https://cdn.test/emblem.png",
      crestBorderUrl: "https://cdn.test/border.png",
    };
    const readGuildDocument = vi.fn().mockResolvedValue(null);
    const listRaiders = vi.fn().mockResolvedValue([
      {
        characters: [
          {
            realm: "test-realm",
            profileSummary: {
              guild: {
                id: 12345,
                name: "Test Guild",
              },
            },
          },
        ],
      },
    ]);
    const fetchGuildProfile = vi.fn().mockResolvedValue({ notModified: false, body: profileSummary, etag: undefined });
    const fetchGuildRoster = vi.fn().mockResolvedValue({ notModified: false, body: rosterSummary, etag: undefined });
    const syncGuildCrestForDocument = vi.fn().mockResolvedValue(crest);
    const upsertGuildDocument = vi.fn(async (doc) => doc);

    const result = await ensureGuildDocumentForAdmin({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument,
      listRaiders,
      fetchGuildProfile,
      fetchGuildRoster,
      syncGuildCrestForDocument,
      upsertGuildDocument,
    });

    expect(readGuildDocument).toHaveBeenCalledWith("12345");
    expect(listRaiders).toHaveBeenCalledTimes(1);
    expect(fetchGuildProfile).toHaveBeenCalledWith("test-realm", "test-guild", "token", undefined);
    expect(fetchGuildRoster).toHaveBeenCalledWith("test-realm", "test-guild", "token", undefined);
    expect(syncGuildCrestForDocument).toHaveBeenCalledWith("12345", profileSummary, "token", null);
    expect(upsertGuildDocument).toHaveBeenCalledWith(expect.objectContaining({
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      profileSummary,
      profileFetchedAt: expect.any(String),
      blizzardProfileRaw: profileSummary,
      blizzardProfileFetchedAt: expect.any(String),
      blizzardRosterRaw: rosterSummary,
      blizzardRosterFetchedAt: expect.any(String),
      crestEmblemUrl: "https://cdn.test/emblem.png",
      crestBorderUrl: "https://cdn.test/border.png",
      rankPermissions: undefined,
      setup: undefined,
      lastOverrideAt: undefined,
      lastOverrideBy: undefined,
    }));
    expect(result).toMatchObject({
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      profileSummary,
      blizzardProfileRaw: profileSummary,
      blizzardRosterRaw: rosterSummary,
      crestEmblemUrl: "https://cdn.test/emblem.png",
      crestBorderUrl: "https://cdn.test/border.png",
    });
  });
});
