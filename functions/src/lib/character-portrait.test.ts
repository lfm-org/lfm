import { describe, expect, it, vi } from "vitest";
import { syncCharacterPortrait } from "./character-portrait.js";

describe("syncCharacterPortrait", () => {
  it("mirrors an avatar asset into blob storage and returns the local portrait URL", async () => {
    const fetchBinaryAsset = vi.fn(async () => ({
      bytes: new Uint8Array([0xff, 0xd8, 0xff]),
      contentType: "image/jpeg",
    }));
    const writeBinaryBlob = vi.fn(async () => {});

    const result = await syncCharacterPortrait(
      "eu-test-realm-aelrin",
      "https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg",
      {
        fetchBinaryAsset,
        writeBinaryBlob,
        getPublicBlobUrl: (blobName) => `https://lfmstore.blob.core.windows.net/wow/${blobName}`,
      }
    );

    expect(fetchBinaryAsset).toHaveBeenCalledWith(
      "https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg"
    );
    expect(writeBinaryBlob).toHaveBeenCalledWith(
      "character-portraits/eu-test-realm-aelrin.jpg",
      new Uint8Array([0xff, 0xd8, 0xff]),
      "image/jpeg"
    );
    expect(result).toEqual({
      portraitBlobName: "character-portraits/eu-test-realm-aelrin.jpg",
      portraitUrl: "https://lfmstore.blob.core.windows.net/wow/character-portraits/eu-test-realm-aelrin.jpg",
    });
  });
});
