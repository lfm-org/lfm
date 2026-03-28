import { describe, expect, it } from "vitest";
import {
  normalizePortraitMap,
  normalizePortraitUrl,
  normalizePortraitUrlField,
} from "./portraitUrls";

describe("normalizePortraitUrl", () => {
  it("resolves relative portrait URLs against an absolute API base URL", () => {
    expect(
      normalizePortraitUrl(
        "/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
        "https://lfm-api.dinosauruskeksi.com/api",
      ),
    ).toBe("https://lfm-api.dinosauruskeksi.com/api/raider/character-portrait/eu-stormreaver-aelrin/jpg");
  });

  it("preserves relative portrait URLs when the API base URL is same-origin relative", () => {
    expect(
      normalizePortraitUrl(
        "/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
        "/api",
      ),
    ).toBe("/api/raider/character-portrait/eu-stormreaver-aelrin/jpg");
  });

  it("leaves absolute portrait URLs unchanged", () => {
    expect(
      normalizePortraitUrl(
        "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
        "https://lfm-api.dinosauruskeksi.com/api",
      ),
    ).toBe("https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg");
  });
});

describe("normalizePortraitUrlField", () => {
  it("normalizes portraitUrl on object payloads", () => {
    expect(
      normalizePortraitUrlField(
        {
          id: "eu-stormreaver-aelrin",
          portraitUrl: "/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
        },
        "https://lfm-api.dinosauruskeksi.com/api",
      ),
    ).toEqual({
      id: "eu-stormreaver-aelrin",
      portraitUrl: "https://lfm-api.dinosauruskeksi.com/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
    });
  });
});

describe("normalizePortraitMap", () => {
  it("normalizes portrait maps returned by the portrait lookup endpoint", () => {
    expect(
      normalizePortraitMap(
        {
          "eu-stormreaver-aelrin": "/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
        },
        "https://lfm-api.dinosauruskeksi.com/api",
      ),
    ).toEqual({
      "eu-stormreaver-aelrin": "https://lfm-api.dinosauruskeksi.com/api/raider/character-portrait/eu-stormreaver-aelrin/jpg",
    });
  });
});
