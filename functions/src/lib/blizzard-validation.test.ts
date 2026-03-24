import { describe, it, expect } from "vitest";
import { validateRegion, validateRealmSlug, validateCharacterName, encodeBlizzardPathSegments } from "./blizzard-validation.js";

describe("validateRegion", () => {
  it("accepts valid regions", () => {
    expect(validateRegion("eu")).toBe("eu");
    expect(validateRegion("us")).toBe("us");
    expect(validateRegion("kr")).toBe("kr");
    expect(validateRegion("tw")).toBe("tw");
    expect(validateRegion("cn")).toBe("cn");
  });

  it("rejects invalid regions", () => {
    expect(() => validateRegion("evil.com")).toThrow();
    expect(() => validateRegion("")).toThrow();
    expect(() => validateRegion("EU")).toThrow();
  });
});

describe("validateRealmSlug", () => {
  it("accepts valid realm slugs", () => {
    expect(validateRealmSlug("test-realm")).toBe("test-realm");
    expect(validateRealmSlug("stormrage")).toBe("stormrage");
  });

  it("rejects invalid realm slugs", () => {
    expect(() => validateRealmSlug("")).toThrow();
    expect(() => validateRealmSlug("realm/evil")).toThrow();
    expect(() => validateRealmSlug("realm with spaces")).toThrow();
    expect(() => validateRealmSlug("a".repeat(65))).toThrow();
  });
});

describe("validateCharacterName", () => {
  it("accepts valid names and lowercases them", () => {
    expect(validateCharacterName("Aelrin")).toBe("aelrin");
    expect(validateCharacterName("ab")).toBe("ab");
    expect(validateCharacterName("Éloïse")).toBe("éloïse");
  });

  it("rejects invalid names", () => {
    expect(() => validateCharacterName("")).toThrow();
    expect(() => validateCharacterName("a")).toThrow();
    expect(() => validateCharacterName("a".repeat(13))).toThrow();
    expect(() => validateCharacterName("name123")).toThrow();
    expect(() => validateCharacterName("name/evil")).toThrow();
  });
});

describe("encodeBlizzardPathSegments", () => {
  it("encodes and joins segments", () => {
    expect(encodeBlizzardPathSegments("test-realm", "aelrin")).toBe("test-realm/aelrin");
  });

  it("encodes special characters", () => {
    expect(encodeBlizzardPathSegments("realm/evil", "name")).toBe("realm%2Fevil/name");
  });
});
