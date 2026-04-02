import { describe, expect, it } from "vitest";
import { getServedCharacterPortraitUrl, findAvatarUrl, isBlizzardRenderUrl } from "./character-portrait.js";

describe("isBlizzardRenderUrl", () => {
  it("returns true for Blizzard CDN URLs", () => {
    expect(isBlizzardRenderUrl("https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg")).toBe(true);
  });

  it("returns false for other URLs", () => {
    expect(isBlizzardRenderUrl("https://example.com/portrait.jpg")).toBe(false);
    expect(isBlizzardRenderUrl(null)).toBe(false);
    expect(isBlizzardRenderUrl(undefined)).toBe(false);
  });
});

describe("findAvatarUrl", () => {
  it("returns the avatar asset value from a media summary", () => {
    expect(
      findAvatarUrl({
        assets: [
          { key: "main", value: "https://render.worldofwarcraft.com/eu/character/test-realm/101/main.jpg" },
          { key: "avatar", value: "https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg" },
        ],
      })
    ).toBe("https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg");
  });

  it("returns empty string when no avatar asset is present", () => {
    expect(findAvatarUrl({ assets: [{ key: "main", value: "https://example.com/main.jpg" }] })).toBe("");
    expect(findAvatarUrl(null)).toBe("");
    expect(findAvatarUrl(undefined)).toBe("");
  });
});

describe("getServedCharacterPortraitUrl", () => {
  it("prefers the avatar URL from mediaSummary over portraitUrl", () => {
    const result = getServedCharacterPortraitUrl(
      "https://render.worldofwarcraft.com/eu/character/test-realm/101/old.jpg",
      { assets: [{ key: "avatar", value: "https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg" }] }
    );
    expect(result).toBe("https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg");
  });

  it("falls back to portraitUrl when mediaSummary has no avatar", () => {
    const result = getServedCharacterPortraitUrl(
      "https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg",
      null
    );
    expect(result).toBe("https://render.worldofwarcraft.com/eu/character/test-realm/101/avatar.jpg");
  });

  it("returns empty string when neither source has a CDN URL", () => {
    expect(getServedCharacterPortraitUrl(null, null)).toBe("");
    expect(getServedCharacterPortraitUrl(undefined, undefined)).toBe("");
    expect(getServedCharacterPortraitUrl("https://not-a-blizzard-url.com/avatar.jpg", null)).toBe("");
  });
});
