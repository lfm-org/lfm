import { describe, expect, it } from "vitest";
import {
  buildDefaultRankPermissions,
  getEffectiveGuildPermissions,
  isGuildRosterFresh,
  mergeRankPermissions,
} from "./guild-permissions.js";
import type { GuildDocument, RaiderDocument } from "../types/index.js";
import type { BlizzardGuildRosterResponse } from "../types/blizzard.js";

function createRoster(): BlizzardGuildRosterResponse {
  return {
    guild: {
      id: 12345,
      name: "Test Guild",
      realm: {
        id: 559,
        slug: "test-realm",
        name: { en_US: "Test Realm" },
      },
      faction: { type: "ALLIANCE", name: "Alliance" },
    },
    members: [
      {
        character: {
          id: 201,
          name: "Highlord",
          realm: { id: 559, slug: "test-realm", name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 2 },
          playable_race: { id: 3 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 0,
      },
      {
        character: {
          id: 101,
          name: "Aelrin",
          realm: { id: 559, slug: "test-realm", name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 2 },
          playable_race: { id: 11 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 2,
      },
      {
        character: {
          id: 102,
          name: "Brakka",
          realm: { id: 559, slug: "test-realm", name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 1 },
          playable_race: { id: 2 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 5,
      },
    ],
  };
}

function createRaider(name: string): RaiderDocument {
  return {
    id: `bnet-${name.toLowerCase()}`,
    battleNetId: `bnet-${name.toLowerCase()}`,
    selectedCharacterId: `eu-test-realm-${name.toLowerCase()}`,
    createdAt: "2026-03-25T10:00:00.000Z",
    lastSeenAt: "2026-03-25T10:00:00.000Z",
    characters: [
      {
        id: `eu-test-realm-${name.toLowerCase()}`,
        region: "eu",
        realm: "test-realm",
        name,
        fetchedAt: "2026-03-25T10:00:00.000Z",
        profileSummary: {
          id: name === "Highlord" ? 201 : name === "Aelrin" ? 101 : name === "Brakka" ? 102 : undefined,
          name,
          level: 80,
          realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
          character_class: { id: 2, name: "" },
          race: { id: 1, name: "" },
          guild: { id: 12345, name: "Test Guild" },
        },
      },
    ],
  };
}

function createGuildDoc(overrides: Partial<GuildDocument> = {}): GuildDocument {
  return {
    id: "12345",
    guildId: 12345,
    realmSlug: "test-realm",
    blizzardRosterRaw: createRoster(),
    blizzardRosterFetchedAt: "2026-03-25T10:00:00.000Z",
    blizzardProfileRaw: {
      id: 12345,
      name: "Test Guild",
      realm: {
        id: 559,
        slug: "test-realm",
        name: { en_US: "Test Realm" },
      },
      faction: { type: "ALLIANCE", name: "Alliance" },
      member_count: 3,
      achievement_points: 10,
    },
    blizzardProfileFetchedAt: "2026-03-25T10:00:00.000Z",
    ...overrides,
  };
}

describe("guild permissions", () => {
  it("builds default permissions with signup enabled for every rank and create enabled only for rank 0", () => {
    expect(buildDefaultRankPermissions([0, 2, 5])).toEqual([
      { rank: 0, canCreateGuildRuns: true, canSignupGuildRuns: true, canDeleteGuildRuns: true },
      { rank: 2, canCreateGuildRuns: false, canSignupGuildRuns: true, canDeleteGuildRuns: false },
      { rank: 5, canCreateGuildRuns: false, canSignupGuildRuns: true, canDeleteGuildRuns: false },
    ]);
  });

  it("merges stored permissions onto the current roster ranks", () => {
    expect(
      mergeRankPermissions([0, 2, 5], [
        { rank: 2, canCreateGuildRuns: true, canSignupGuildRuns: false },
      ])
    ).toEqual([
      { rank: 0, canCreateGuildRuns: true, canSignupGuildRuns: true, canDeleteGuildRuns: true },
      { rank: 2, canCreateGuildRuns: true, canSignupGuildRuns: false, canDeleteGuildRuns: false },
      { rank: 5, canCreateGuildRuns: false, canSignupGuildRuns: true, canDeleteGuildRuns: false },
    ]);
  });

  it("grants default signup but not guild raid creation to a rank 2 member", () => {
    const permissions = getEffectiveGuildPermissions(createGuildDoc(), createRaider("Aelrin"), Date.parse("2026-03-25T10:30:00.000Z"));
    expect(permissions.matchedRank).toBe(2);
    expect(permissions.canCreateGuildRuns).toBe(false);
    expect(permissions.canSignupGuildRuns).toBe(true);
  });

  it("matches a member by canonical profile data when stored top-level fields drift", () => {
    const raider = createRaider("Aelrin");
    raider.characters[0].realm = "old-realm";
    raider.characters[0].name = "Aelrinn";

    const permissions = getEffectiveGuildPermissions(
      createGuildDoc({
        rankPermissions: [{ rank: 2, canCreateGuildRuns: true, canSignupGuildRuns: true }],
      }),
      raider,
      Date.parse("2026-03-25T10:30:00.000Z"),
    );

    expect(permissions.matchedRank).toBe(2);
    expect(permissions.canCreateGuildRuns).toBe(true);
    expect(permissions.canSignupGuildRuns).toBe(true);
  });

  it("fails closed when roster data is stale", () => {
    const staleDoc = createGuildDoc({
      blizzardRosterFetchedAt: "2026-03-25T07:00:00.000Z",
    });
    expect(isGuildRosterFresh(staleDoc, Date.parse("2026-03-25T10:30:00.000Z"))).toBe(false);

    const permissions = getEffectiveGuildPermissions(staleDoc, createRaider("Highlord"), Date.parse("2026-03-25T10:30:00.000Z"));
    expect(permissions.canCreateGuildRuns).toBe(false);
    expect(permissions.canSignupGuildRuns).toBe(false);
    expect(permissions.canDeleteGuildRuns).toBe(false);
  });

  it("merges stored canDeleteGuildRuns onto roster ranks", () => {
    expect(
      mergeRankPermissions([0, 2, 5], [
        { rank: 2, canCreateGuildRuns: true, canSignupGuildRuns: false, canDeleteGuildRuns: true },
      ])
    ).toEqual([
      { rank: 0, canCreateGuildRuns: true, canSignupGuildRuns: true, canDeleteGuildRuns: true },
      { rank: 2, canCreateGuildRuns: true, canSignupGuildRuns: false, canDeleteGuildRuns: true },
      { rank: 5, canCreateGuildRuns: false, canSignupGuildRuns: true, canDeleteGuildRuns: false },
    ]);
  });

  it("resolves canDeleteGuildRuns in effective permissions for rank 0", () => {
    const permissions = getEffectiveGuildPermissions(createGuildDoc(), createRaider("Highlord"), Date.parse("2026-03-25T10:30:00.000Z"));
    expect(permissions.matchedRank).toBe(0);
    expect(permissions.canDeleteGuildRuns).toBe(true);
  });

  it("resolves canDeleteGuildRuns as false for non-zero rank by default", () => {
    const permissions = getEffectiveGuildPermissions(createGuildDoc(), createRaider("Aelrin"), Date.parse("2026-03-25T10:30:00.000Z"));
    expect(permissions.matchedRank).toBe(2);
    expect(permissions.canDeleteGuildRuns).toBe(false);
  });
});
