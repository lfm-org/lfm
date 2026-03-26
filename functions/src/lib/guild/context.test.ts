import { describe, expect, it } from "vitest";
import { parseGuildId, resolveGuildEditor, resolveRealmSlug, toGuildNameSlug } from "./context.js";

describe("parseGuildId", () => {
  it("accepts numeric ids and trims whitespace", () => {
    expect(parseGuildId(" 12345 ")).toBe("12345");
  });

  it("rejects non-numeric ids", () => {
    expect(parseGuildId("guild-123")).toBeNull();
  });
});

describe("toGuildNameSlug", () => {
  it("normalizes guild names to Blizzard-friendly slugs", () => {
    expect(toGuildNameSlug("Knights of Ni!")).toBe("knights-of-ni");
  });
});

describe("resolveRealmSlug", () => {
  it("uses the selected character realm when present", () => {
    expect(resolveRealmSlug({
      selectedCharacterId: "eu-test-realm-aelrin",
      characters: [
        { id: "eu-test-realm-aelrin", realm: "test-realm", name: "Aelrin" },
      ],
    } as never)).toBe("test-realm");
  });

  it("falls back to the first character when the selected character is missing", () => {
    expect(resolveRealmSlug({
      selectedCharacterId: "missing",
      characters: [
        { id: "first", realm: "fallback-realm", name: "Aelrin" },
        { id: "second", realm: "other-realm", name: "Bran" },
      ],
    } as never)).toBe("fallback-realm");
  });
});

describe("resolveGuildEditor", () => {
  it("returns guild-master access for rank zero", () => {
    const resolution = resolveGuildEditor(
      {
        characters: [{ id: "a", realm: "test-realm", name: "Aelrin" }],
      } as never,
      {
        members: [
          { rank: 0, character: { name: "Aelrin", realm: { slug: "test-realm" } } },
        ],
      } as never,
    );

    expect(resolution).toEqual({ canEdit: true, mode: "guild-master", matchedRank: 0 });
  });

  it("returns member access for non-guild-master matches", () => {
    const resolution = resolveGuildEditor(
      {
        characters: [{ id: "a", realm: "test-realm", name: "Aelrin" }],
      } as never,
      {
        members: [
          { rank: 2, character: { name: "Aelrin", realm: { slug: "test-realm" } } },
        ],
      } as never,
    );

    expect(resolution).toEqual({ canEdit: false, mode: "member", matchedRank: 2 });
  });

  it("returns member access when no roster match exists", () => {
    const resolution = resolveGuildEditor(
      {
        characters: [{ id: "a", realm: "test-realm", name: "Aelrin" }],
      } as never,
      {
        members: [
          { rank: 0, character: { name: "Someone Else", realm: { slug: "test-realm" } } },
        ],
      } as never,
    );

    expect(resolution).toEqual({ canEdit: false, mode: "member", matchedRank: null });
  });
});
