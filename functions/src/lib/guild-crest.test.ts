import { describe, expect, it, vi } from "vitest";
import { syncGuildCrest } from "./guild-crest.js";

describe("syncGuildCrest", () => {
  it("mirrors emblem and border assets locally and returns an app-served composite crest URL", async () => {
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
    const fetchBinaryAsset = vi.fn(async (url: string) => ({
      contentType: "image/png",
      bytes: new Uint8Array(Buffer.from(url)),
    }));
    const writeBinaryBlob = vi.fn(async () => {});

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
      fetchBinaryAsset,
      writeBinaryBlob,
      now: "2026-03-25T12:00:00.000Z",
    });

    expect(result).toMatchObject({
      crestUrl: "/api/guild/12345/crest",
      crestBlobName: "guild-crests/12345/crest.svg",
      crestEmblemBlobName: "guild-crests/12345/emblem.png",
      crestBorderBlobName: "guild-crests/12345/border.png",
      blizzardCrestMediaFetchedAt: "2026-03-25T12:00:00.000Z",
      blizzardCrestEmblemMediaRaw: {
        assets: [{ key: "icon", value: "https://cdn.example.test/emblem-50.png" }],
      },
      blizzardCrestBorderMediaRaw: {
        assets: [{ key: "icon", value: "https://cdn.example.test/border-1.png" }],
      },
    });

    expect(fetchMediaDocument).toHaveBeenCalledTimes(2);
    expect(fetchBinaryAsset).toHaveBeenCalledWith("https://cdn.example.test/emblem-50.png");
    expect(fetchBinaryAsset).toHaveBeenCalledWith("https://cdn.example.test/border-1.png");
    expect(writeBinaryBlob).toHaveBeenCalledTimes(3);
    expect(writeBinaryBlob).toHaveBeenCalledWith(
      "guild-crests/12345/emblem.png",
      expect.any(Uint8Array),
      "image/png"
    );
    expect(writeBinaryBlob).toHaveBeenCalledWith(
      "guild-crests/12345/border.png",
      expect.any(Uint8Array),
      "image/png"
    );
    expect(writeBinaryBlob).toHaveBeenCalledWith(
      "guild-crests/12345/crest.svg",
      expect.any(Uint8Array),
      "image/svg+xml"
    );

    const crestSvgBytes = writeBinaryBlob.mock.calls[2]?.[1] as Uint8Array;
    const crestSvg = Buffer.from(crestSvgBytes).toString("utf-8");
    expect(crestSvg).toContain("data:image/png;base64,");
    expect(crestSvg).not.toContain("https://blob.example.test");
  });
});
