import { describe, expect, it } from "vitest";
import {
  normalizePortraitMap,
  normalizePortraitUrl,
  normalizePortraitUrlField,
} from "./portraitUrls";

const CDN_URL = "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg";

describe("normalizePortraitUrl", () => {
  it("returns the CDN URL as-is", () => {
    expect(normalizePortraitUrl(CDN_URL)).toBe(CDN_URL);
  });

  it("returns undefined for null input", () => {
    expect(normalizePortraitUrl(null)).toBeUndefined();
  });

  it("returns undefined for undefined input", () => {
    expect(normalizePortraitUrl(undefined)).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(normalizePortraitUrl("")).toBeUndefined();
  });
});

describe("normalizePortraitUrlField", () => {
  it("returns the object unchanged when portraitUrl is a CDN URL", () => {
    const input = { id: "eu-stormreaver-aelrin", portraitUrl: CDN_URL };
    expect(normalizePortraitUrlField(input)).toEqual(input);
  });

  it("returns the object unchanged when portraitUrl is absent", () => {
    const input = { id: "eu-stormreaver-aelrin" };
    expect(normalizePortraitUrlField(input)).toEqual(input);
  });
});

describe("normalizePortraitMap", () => {
  it("returns the portrait map unchanged", () => {
    const input = { "eu-stormreaver-aelrin": CDN_URL };
    expect(normalizePortraitMap(input)).toEqual(input);
  });
});
