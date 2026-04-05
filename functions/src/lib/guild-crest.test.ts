import { describe, expect, it, vi } from "vitest";
import { syncGuildCrest } from "./guild-crest.js";

const emblemMedia = { assets: [{ key: "icon", value: "https://cdn.example.test/emblem-50.png" }] };
const borderMedia = { assets: [{ key: "icon", value: "https://cdn.example.test/border-1.png" }] };

const crestProfile = {
  id: 12345,
  name: "Test Guild",
  realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
  crest: {
    emblem: {
      id: 50,
      media: { key: { href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/emblem/50?namespace=static-eu" } },
      color: { rgba: { r: 220, g: 160, b: 80, a: 1 } },
    },
    border: {
      id: 1,
      media: { key: { href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/border/1?namespace=static-eu" } },
      color: { rgba: { r: 249, g: 204, b: 48, a: 1 } },
    },
    background: {
      color: { rgba: { r: 158, g: 0, b: 54, a: 1 } },
    },
  },
};

describe("syncGuildCrest", () => {
  it("fetches media documents and returns CDN URLs for emblem and border", async () => {
    const fetchMediaDocument = vi.fn(async (href: string) => {
      if (href.includes("/emblem/50")) {
        return { notModified: false as const, body: emblemMedia, etag: "W/\"emblem-etag\"" };
      }
      return { notModified: false as const, body: borderMedia, etag: "W/\"border-etag\"" };
    });

    const result = await syncGuildCrest("12345", crestProfile as never, {
      fetchMediaDocument,
      now: "2026-03-25T12:00:00.000Z",
    });

    expect(result).toMatchObject({
      crestEmblemUrl: "https://cdn.example.test/emblem-50.png",
      crestBorderUrl: "https://cdn.example.test/border-1.png",
      blizzardCrestMediaFetchedAt: "2026-03-25T12:00:00.000Z",
      blizzardCrestEmblemMediaRaw: emblemMedia,
      blizzardCrestBorderMediaRaw: borderMedia,
      emblemEtag: "W/\"emblem-etag\"",
      borderEtag: "W/\"border-etag\"",
    });
    expect(fetchMediaDocument).toHaveBeenCalledTimes(2);
  });

  it("sends If-None-Match when cached etags are provided", async () => {
    const fetchMediaDocument = vi.fn(async (_href: string, _etag?: string) =>
      ({ notModified: false as const, body: emblemMedia, etag: "W/\"new-etag\"" })
    );

    await syncGuildCrest("12345", crestProfile as never, {
      fetchMediaDocument,
      cachedEmblemEtag: "W/\"emblem-cached\"",
      cachedBorderEtag: "W/\"border-cached\"",
      cachedEmblemMedia: emblemMedia as never,
      cachedBorderMedia: borderMedia as never,
      now: "2026-03-25T12:00:00.000Z",
    });

    expect(fetchMediaDocument).toHaveBeenCalledWith(
      expect.stringContaining("/emblem/50"),
      "W/\"emblem-cached\""
    );
    expect(fetchMediaDocument).toHaveBeenCalledWith(
      expect.stringContaining("/border/1"),
      "W/\"border-cached\""
    );
  });

  it("uses cached media on 304 response", async () => {
    const cachedEmblem = { assets: [{ key: "icon", value: "https://cdn.example.test/emblem-cached.png" }] };
    const cachedBorder = { assets: [{ key: "icon", value: "https://cdn.example.test/border-cached.png" }] };
    const fetchMediaDocument = vi.fn(async () =>
      ({ notModified: true as const, etag: "W/\"cached-etag\"" })
    );

    const result = await syncGuildCrest("12345", crestProfile as never, {
      fetchMediaDocument,
      cachedEmblemEtag: "W/\"cached-etag\"",
      cachedBorderEtag: "W/\"cached-etag\"",
      cachedEmblemMedia: cachedEmblem as never,
      cachedBorderMedia: cachedBorder as never,
      now: "2026-03-25T12:00:00.000Z",
    });

    expect(result).toMatchObject({
      crestEmblemUrl: "https://cdn.example.test/emblem-cached.png",
      crestBorderUrl: "https://cdn.example.test/border-cached.png",
      blizzardCrestEmblemMediaRaw: cachedEmblem,
      blizzardCrestBorderMediaRaw: cachedBorder,
      emblemEtag: "W/\"cached-etag\"",
      borderEtag: "W/\"cached-etag\"",
    });
  });

  it("returns null when guild profile has no crest data", async () => {
    const fetchMediaDocument = vi.fn();

    const result = await syncGuildCrest("12345", {
      id: 12345,
      name: "Test Guild",
      realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
    }, {
      fetchMediaDocument,
    });

    expect(result).toBeNull();
    expect(fetchMediaDocument).not.toHaveBeenCalled();
  });

  it("returns null when media documents have no asset URLs", async () => {
    const fetchMediaDocument = vi.fn(async () =>
      ({ notModified: false as const, body: { assets: [] }, etag: undefined })
    );

    const result = await syncGuildCrest("12345", {
      id: 12345,
      name: "Test Guild",
      realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
      crest: {
        emblem: {
          id: 50,
          media: { key: { href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/emblem/50?namespace=static-eu" } },
          color: { rgba: { r: 0, g: 0, b: 0, a: 1 } },
        },
        border: {
          id: 1,
          media: { key: { href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/border/1?namespace=static-eu" } },
          color: { rgba: { r: 0, g: 0, b: 0, a: 1 } },
        },
        background: { color: { rgba: { r: 0, g: 0, b: 0, a: 1 } } },
      },
    }, {
      fetchMediaDocument,
    });

    expect(result).toBeNull();
  });
});
