import { describe, expect, it } from "vitest";
import {
  toAccountCharacterViews,
  toBattleNetIdentity,
  toGuildHomeView,
  toSelectedCharacterView,
  toWowClassViews,
  toWowInstanceViews,
  toWowRaceViews,
  toWowSpecializationViews,
} from "./blizzard-adapters.js";
import type { GuildDocument, StoredSelectedCharacter } from "../types/index.js";

describe("blizzard-adapters", () => {
  it("adapts raw reference documents into app view models", () => {
    const classIndex = {
      _links: { self: { href: "https://example.test/class/index" } },
      classes: [
        { key: { href: "https://example.test/class/1" }, id: 1, name: "Warrior" },
        { key: { href: "https://example.test/class/2" }, id: 2, name: "Paladin" },
      ],
    };
    const classDetails = new Map([
      [1, { id: 1, name: "Warrior" }],
      [2, { id: 2, name: "Paladin" }],
    ]);

    const raceIndex = {
      _links: { self: { href: "https://example.test/race/index" } },
      races: [
        { key: { href: "https://example.test/race/1" }, id: 1, name: "Human" },
        { key: { href: "https://example.test/race/2" }, id: 2, name: "Orc" },
      ],
    };
    const raceDetails = new Map([
      [1, { id: 1, name: "Human", faction: { type: "ALLIANCE", name: "Alliance" } }],
      [2, { id: 2, name: "Orc", faction: { type: "HORDE", name: "Horde" } }],
    ]);

    const specializationIndex = {
      _links: { self: { href: "https://example.test/spec/index" } },
      character_specializations: [
        { key: { href: "https://example.test/spec/65" }, id: 65, name: "Holy" },
        { key: { href: "https://example.test/spec/66" }, id: 66, name: "Protection" },
      ],
    };
    const specializationDetails = new Map([
      [65, { id: 65, name: "Holy", playable_class: { id: 2, name: "Paladin" }, role: { type: "HEALER", name: "Healer" } }],
      [66, { id: 66, name: "Protection", playable_class: { id: 2, name: "Paladin" }, role: { type: "TANK", name: "Tank" } }],
    ]);

    const instanceIndex = {
      _links: { self: { href: "https://example.test/instance/index" } },
      instances: [
        { key: { href: "https://example.test/instance/63" }, id: 63, name: { en_US: "Deadmines" } },
      ],
    };
    const instanceDetails = new Map([
      [
        63,
        {
          id: 63,
          name: "Deadmines",
          category: { type: "DUNGEON" },
          expansion: { id: 1, name: "Classic" },
          minimum_level: 10,
          modes: [
            {
              mode: { type: "NORMAL", name: "Normal" },
              players: 5,
              is_tracked: true,
            },
          ],
        },
      ],
    ]);

    expect(toWowClassViews(classIndex, classDetails)).toEqual([
      { id: 1, name: "Warrior" },
      { id: 2, name: "Paladin" },
    ]);

    expect(toWowRaceViews(raceIndex, raceDetails)).toEqual([
      { id: 1, faction: "ALLIANCE", name: "Human" },
      { id: 2, faction: "HORDE", name: "Orc" },
    ]);

    expect(toWowSpecializationViews(specializationIndex, specializationDetails)).toEqual([
      { id: 65, name: "Holy", classId: 2, role: "HEALER" },
      { id: 66, name: "Protection", classId: 2, role: "TANK" },
    ]);

    expect(toWowInstanceViews(instanceIndex, instanceDetails)).toEqual([
      {
        id: 63,
        name: "Deadmines",
        type: "DUNGEON",
        minLevel: 10,
        expansionId: 1,
        modes: [
          {
            mode: { type: "NORMAL", name: "Normal" },
            players: 5,
            is_tracked: true,
          },
        ],
      },
    ]);
  });

  it("adapts raw account and selected-character documents into app views", () => {
    const accountProfileSummary = {
      wow_accounts: [
        {
          id: 1,
          characters: [
            {
              id: 101,
              name: "Aelrin",
              level: 80,
              realm: { id: 1305, slug: "test-realm", name: { en_US: "Test Realm" } },
              playable_class: { id: 2, name: "Paladin" },
              playable_race: { id: 11, name: "Draenei" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "FEMALE", name: "Female" },
            },
          ],
        },
      ],
    };

    expect(toAccountCharacterViews(accountProfileSummary, "eu")).toEqual([
      {
        name: "Aelrin",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
        classId: 2,
      },
    ]);

    // portraitCache fallback
    const cache = { "eu-test-realm-aelrin": "https://example.test/cached-avatar.jpg" };
    expect(toAccountCharacterViews(accountProfileSummary, "eu", [], cache)).toEqual([
      {
        name: "Aelrin",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
        classId: 2,
        portraitUrl: "https://example.test/cached-avatar.jpg",
      },
    ]);

    const storedCharacter = {
      id: "eu-test-realm-aelrin",
      region: "eu",
      realm: "test-realm",
      name: "Aelrin",
      portraitBlobName: "character-portraits/eu-test-realm-aelrin.png",
      portraitUrl: "https://lfmstore.blob.core.windows.net/wow/character-portraits/eu-test-realm-aelrin.png",
      fetchedAt: "2026-03-20T10:00:00.000Z",
      profileSummary: {
        name: "Aelrin",
        level: 80,
        realm: { id: 1305, slug: "test-realm", name: { en_US: "Test Realm" } },
        character_class: { id: 2, name: "Paladin" },
        race: { id: 11, name: "Draenei" },
      },
      mediaSummary: {
        assets: [
          { key: "avatar", value: "https://example.test/aelrin-avatar.jpg", file_data_id: 123 },
        ],
      },
      specializationsSummary: {
        specializations: [
          { specialization: { id: 65, name: "Holy" } },
          { specialization: { id: 66, name: "Protection" } },
        ],
        active_specialization: { id: 65, name: "Holy" },
      },
    };

    const staticSpecs = new Map([
      [65, { id: 65, name: "Holy", classId: 2, role: "HEALER" as const }],
      [66, { id: 66, name: "Protection", classId: 2, role: "TANK" as const }],
    ]);

    expect(toSelectedCharacterView(storedCharacter, staticSpecs)).toEqual({
      id: "eu-test-realm-aelrin",
      region: "eu",
      realm: "test-realm",
      name: "Aelrin",
      level: 80,
      classId: 2,
      raceId: 11,
      portraitUrl: "https://lfmstore.blob.core.windows.net/wow/character-portraits/eu-test-realm-aelrin.png",
      fetchedAt: "2026-03-20T10:00:00.000Z",
      specializations: [
        { id: 65, name: "Holy", role: "HEALER" },
        { id: 66, name: "Protection", role: "TANK" },
      ],
      activeSpecId: 65,
    });

    expect(toAccountCharacterViews(accountProfileSummary, "eu", [storedCharacter], cache)).toEqual([
      {
        name: "Aelrin",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
        classId: 2,
        className: "Paladin",
        portraitUrl: "https://lfmstore.blob.core.windows.net/wow/character-portraits/eu-test-realm-aelrin.png",
        activeSpecId: 65,
        specName: "Holy",
      },
    ]);
  });

  it("does not surface legacy Blizzard render URLs from the lightweight portrait cache", () => {
    const accountProfileSummary = {
      wow_accounts: [
        {
          id: 1,
          characters: [
            {
              id: 101,
              name: "Aelrin",
              level: 80,
              realm: { id: 1305, slug: "test-realm", name: { en_US: "Test Realm" } },
              playable_class: { id: 2, name: "Paladin" },
              playable_race: { id: 11, name: "Draenei" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "FEMALE", name: "Female" },
            },
          ],
        },
      ],
    };

    expect(
      toAccountCharacterViews(accountProfileSummary, "eu", [], {
        "eu-test-realm-aelrin": "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
      })
    ).toEqual([
      {
        name: "Aelrin",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
        classId: 2,
      },
    ]);
  });

  it("normalizes localized instance names into plain strings", () => {
    const instanceIndex = {
      _links: { self: { href: "https://example.test/instance/index" } },
      instances: [
        { key: { href: "https://example.test/instance/2522" }, id: 2522, name: { en_US: "Liberation of Undermine" } },
      ],
    };
    const instanceDetails = new Map([
      [
        2522,
        {
          id: 2522,
          name: { en_US: "Liberation of Undermine", fr_FR: "Libération de Terremine" },
          category: { type: "RAID" },
          expansion: { id: 10, name: "The War Within" },
          minimum_level: 80,
          modes: [
            {
              mode: {
                type: "NORMAL",
                name: { en_US: "Normal", de_DE: "Normal" },
              },
              players: 30,
              is_tracked: true,
            },
          ],
        },
      ],
    ]);

    expect(toWowInstanceViews(instanceIndex, instanceDetails)).toEqual([
      {
        id: 2522,
        name: "Liberation of Undermine",
        type: "RAID",
        minLevel: 80,
        expansionId: 10,
        modes: [
          {
            mode: {
              type: "NORMAL",
              name: "Normal",
            },
            players: 30,
            is_tracked: true,
          },
        ],
      },
    ]);
  });
});

describe("toBattleNetIdentity", () => {
  it("returns guild from selected character profileSummary", () => {
    const character = {
      id: "eu-test-realm-aelrin",
      region: "eu",
      realm: "test-realm",
      name: "Aelrin",
      profileSummary: {
        name: "Aelrin",
        level: 80,
        realm: { slug: "test-realm", name: "Test Realm" },
        character_class: { id: 2, name: "Paladin" },
        race: { id: 11, name: "Draenei" },
        guild: { id: 12345, name: "Test Guild" },
      },
    } as StoredSelectedCharacter;

    expect(toBattleNetIdentity("battle-net-id", character)).toEqual({
      battleNetId: "battle-net-id",
      guildId: 12345,
      guildName: "Test Guild",
    });
  });

  it("returns null guild when character has no guild", () => {
    const character = {
      id: "eu-test-realm-aelrin",
      region: "eu",
      realm: "test-realm",
      name: "Aelrin",
      profileSummary: {
        name: "Aelrin",
        level: 80,
        realm: { slug: "test-realm", name: "Test Realm" },
        character_class: { id: 2, name: "Paladin" },
        race: { id: 11, name: "Draenei" },
      },
    } as StoredSelectedCharacter;

    expect(toBattleNetIdentity("battle-net-id", character)).toEqual({
      battleNetId: "battle-net-id",
      guildId: null,
      guildName: null,
    });
  });

  it("returns null guild when no character is selected", () => {
    expect(toBattleNetIdentity("battle-net-id", null)).toEqual({
      battleNetId: "battle-net-id",
      guildId: null,
      guildName: null,
    });
  });
});

describe("toGuildHomeView", () => {
  it("exposes the optional guild slogan", () => {
    const guildDoc: GuildDocument = {
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      slogan: "Victory or Lunch",
      blizzardProfileRaw: {
        id: 12345,
        name: "Test Guild",
        realm: {
          slug: "test-realm",
          name: { en_US: "Test Realm" },
        },
        faction: { type: "ALLIANCE", name: "Alliance" },
      },
      blizzardRosterRaw: {
        guild: {
          name: "Test Guild",
          id: 12345,
          realm: { slug: "test-realm" },
        },
        members: [],
      },
    };

    expect(toGuildHomeView(guildDoc).guild).toMatchObject({
      slogan: "Victory or Lunch",
    });
  });

  it("exposes a null guild slogan when none is stored", () => {
    const guildDoc: GuildDocument = {
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      blizzardProfileRaw: {
        id: 12345,
        name: "Test Guild",
        realm: {
          slug: "test-realm",
          name: { en_US: "Test Realm" },
        },
        faction: { type: "ALLIANCE", name: "Alliance" },
      },
      blizzardRosterRaw: {
        guild: {
          name: "Test Guild",
          id: 12345,
          realm: { slug: "test-realm" },
        },
        members: [],
      },
    };

    expect(toGuildHomeView(guildDoc).guild).toMatchObject({
      slogan: null,
    });
  });

  it("prefers the app-served crest route when mirrored crest assets were synced locally", () => {
    const guildDoc: GuildDocument = {
      id: "12345",
      guildId: 12345,
      realmSlug: "test-realm",
      blizzardProfileRaw: {
        id: 12345,
        name: "Test Guild",
        realm: {
          slug: "test-realm",
          name: { en_US: "Test Realm" },
        },
        faction: { type: "ALLIANCE", name: "Alliance" },
        crest: {
          background: {
            color: {
              rgba: { r: 12, g: 34, b: 56, a: 1 },
            },
          },
        },
      },
      blizzardProfileFetchedAt: "2026-03-25T10:00:00.000Z",
      blizzardRosterRaw: {
        guild: {
          name: "Test Guild",
          id: 12345,
          realm: { slug: "test-realm" },
        },
        members: [],
      },
      blizzardRosterFetchedAt: new Date().toISOString(),
      setup: {
        initializedAt: "2026-03-25T10:00:00.000Z",
        timezone: "Europe/Helsinki",
      },
      crestBlobName: "guild-crests/12345/crest.svg",
      crestUrl: "https://blob.example.test/wow/guild-crests/12345-composite.png",
    };

    const view = toGuildHomeView(guildDoc);

    expect(view.guild?.crestUrl).toBe("/api/guild/12345/crest");
  });
});
