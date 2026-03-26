import { describe, expect, it, vi } from "vitest";
import { ensureGuildDocumentForAdmin, refreshGuildDocument } from "./document.js";

describe("refreshGuildDocument", () => {
  it("preserves cached setup and permissions while refreshing Blizzard data", async () => {
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
        setup: { timezone: "Europe/Helsinki", initializedAt: "2026-03-20T00:00:00.000Z" },
        rankPermissions: [{ rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false }],
      } as never,
      fetchGuildProfile: vi.fn().mockResolvedValue({ name: "Test Guild" }),
      fetchGuildRoster: vi.fn().mockResolvedValue({ members: [] }),
      syncGuildCrestForDocument: vi.fn().mockResolvedValue({ crestUrl: "https://blob.test/crest.png" }),
      upsertGuildDocument: vi.fn(async (doc) => doc),
    });

    expect(result.setup?.timezone).toBe("Europe/Helsinki");
    expect(result.rankPermissions).toEqual([{ rank: 1, canCreateGuildRaids: true, canSignupGuildRaids: false }]);
  });
});

describe("ensureGuildDocumentForAdmin", () => {
  it("returns null when no raider can supply guild context", async () => {
    const result = await ensureGuildDocumentForAdmin({
      guildDocId: "12345",
      accessToken: "token",
      readGuildDocument: vi.fn().mockResolvedValue(null),
      listRaiders: vi.fn().mockResolvedValue([]),
      fetchGuildProfile: vi.fn(),
      fetchGuildRoster: vi.fn(),
      syncGuildCrestForDocument: vi.fn(),
      upsertGuildDocument: vi.fn(),
    });

    expect(result).toBeNull();
  });
});
