import { describe, expect, it, vi } from "vitest";
import { syncGuildCrest } from "./guild-crest.js";

describe("syncGuildCrest", () => {
  it("fetches media documents and returns CDN URLs for emblem and border", async () => {
    const fetchMediaDocument = vi.fn(async (href: string) => {
      if (href.includes("/emblem/50")) {
        return {
          assets: [{ key: "icon", value: "https://cdn.example.test/emblem-50.png" }],
        };
      }

      return {
        assets: [{ key: "icon", value: "https://cdn.example.test/border-1.png" }],
      };
    });

    const result = await syncGuildCrest("12345", {
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
    }, {
      fetchMediaDocument,
      now: "2026-03-25T12:00:00.000Z",
    });

    expect(result).toMatchObject({
      crestEmblemUrl: "https://cdn.example.test/emblem-50.png",
      crestBorderUrl: "https://cdn.example.test/border-1.png",
      blizzardCrestMediaFetchedAt: "2026-03-25T12:00:00.000Z",
      blizzardCrestEmblemMediaRaw: {
        assets: [{ key: "icon", value: "https://cdn.example.test/emblem-50.png" }],
      },
      blizzardCrestBorderMediaRaw: {
        assets: [{ key: "icon", value: "https://cdn.example.test/border-1.png" }],
      },
    });

    expect(fetchMediaDocument).toHaveBeenCalledTimes(2);
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
    const fetchMediaDocument = vi.fn(async () => ({ assets: [] }));

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
