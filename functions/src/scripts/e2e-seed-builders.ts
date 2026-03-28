import {
  TEST_MODE_IDENTITY,
  TEST_MODE_DELETE_ACCOUNT_IDENTITY,
  TEST_MODE_GUILD_MASTER_IDENTITY,
  TEST_MODE_NEEDS_CHARACTER_IDENTITY,
  TEST_MODE_SITE_ADMIN_IDENTITY,
} from "../lib/test-mode.js";
import { findModeByKey, getModePlayers } from "../lib/wow-instance-modes.js";
import type {
  Character,
  RaiderDocument,
  RaidCharacter,
  RaidDocument,
  WowInstance,
} from "../types/index.js";
import type { E2eScenario, RaidSeedDefinition } from "./e2e-test-data.js";

const TEST_REALM = "test-realm";
const TEST_REALM_NAME = "Test Realm";
const TEST_GUILD_NAME = "Test Guild";
const OUTSIDER_GUILD_ID = 54321;
const OUTSIDER_GUILD_NAME = "Rival Guild";

interface CharacterTemplate {
  classId: number;
  className: string;
  raceId: number;
  raceName: string;
  specializations: NonNullable<Character["specializations"]>;
  activeSpecId: number;
}

interface SeedCharacterMeta {
  character: Character;
  className: string;
  raceName: string;
}

export interface RaiderSeed {
  document: RaiderDocument;
  primary: SeedCharacterMeta;
}

export interface SeedOptions {
  now: string;
  region: string;
  instances: WowInstance[];
  raidDefinitions?: RaidSeedDefinition[];
  scenario?: E2eScenario;
}

export const CHARACTER_TEMPLATES: CharacterTemplate[] = [
  {
    classId: 1,
    className: "Warrior",
    raceId: 1,
    raceName: "Human",
    specializations: [
      { id: 73, name: "Protection", role: "TANK" },
      { id: 72, name: "Fury", role: "DPS" },
    ],
    activeSpecId: 73,
  },
  {
    classId: 2,
    className: "Paladin",
    raceId: 3,
    raceName: "Dwarf",
    specializations: [
      { id: 70, name: "Retribution", role: "DPS" },
      { id: 66, name: "Protection", role: "TANK" },
    ],
    activeSpecId: 70,
  },
  {
    classId: 2,
    className: "Paladin",
    raceId: 11,
    raceName: "Draenei",
    specializations: [
      { id: 65, name: "Holy", role: "HEALER" },
      { id: 66, name: "Protection", role: "TANK" },
    ],
    activeSpecId: 65,
  },
  {
    classId: 5,
    className: "Priest",
    raceId: 1,
    raceName: "Human",
    specializations: [
      { id: 256, name: "Discipline", role: "HEALER" },
      { id: 258, name: "Shadow", role: "DPS" },
    ],
    activeSpecId: 256,
  },
  {
    classId: 7,
    className: "Shaman",
    raceId: 2,
    raceName: "Orc",
    specializations: [
      { id: 264, name: "Restoration", role: "HEALER" },
      { id: 262, name: "Elemental", role: "DPS" },
    ],
    activeSpecId: 264,
  },
  {
    classId: 8,
    className: "Mage",
    raceId: 7,
    raceName: "Gnome",
    specializations: [
      { id: 63, name: "Fire", role: "DPS" },
      { id: 64, name: "Frost", role: "DPS" },
    ],
    activeSpecId: 63,
  },
  {
    classId: 11,
    className: "Druid",
    raceId: 4,
    raceName: "Night Elf",
    specializations: [
      { id: 104, name: "Guardian", role: "TANK" },
      { id: 102, name: "Balance", role: "DPS" },
    ],
    activeSpecId: 104,
  },
  {
    classId: 3,
    className: "Hunter",
    raceId: 3,
    raceName: "Dwarf",
    specializations: [
      { id: 254, name: "Marksmanship", role: "DPS" },
      { id: 255, name: "Survival", role: "DPS" },
    ],
    activeSpecId: 254,
  },
  {
    classId: 6,
    className: "Death Knight",
    raceId: 5,
    raceName: "Undead",
    specializations: [
      { id: 250, name: "Blood", role: "TANK" },
      { id: 252, name: "Unholy", role: "DPS" },
    ],
    activeSpecId: 250,
  },
  {
    classId: 10,
    className: "Monk",
    raceId: 1,
    raceName: "Human",
    specializations: [
      { id: 268, name: "Brewmaster", role: "TANK" },
      { id: 270, name: "Mistweaver", role: "HEALER" },
    ],
    activeSpecId: 268,
  },
  {
    classId: 9,
    className: "Warlock",
    raceId: 10,
    raceName: "Blood Elf",
    specializations: [
      { id: 265, name: "Affliction", role: "DPS" },
      { id: 267, name: "Destruction", role: "DPS" },
    ],
    activeSpecId: 265,
  },
  {
    classId: 11,
    className: "Druid",
    raceId: 6,
    raceName: "Tauren",
    specializations: [
      { id: 105, name: "Restoration", role: "HEALER" },
      { id: 102, name: "Balance", role: "DPS" },
    ],
    activeSpecId: 105,
  },
];

const NAME_PREFIXES = [
  "Cael",
  "Dorn",
  "Eira",
  "Fenn",
  "Garr",
  "Hale",
  "Iria",
  "Jorn",
  "Kael",
  "Lysa",
];

const NAME_SUFFIXES = ["dor", "wyn", "mere", "thorn"];
const ATTENDANCE_CYCLE: Array<RaidCharacter["desiredAttendance"]> = ["IN", "IN", "IN", "BENCH", "LATE", "AWAY", "OUT"];

export function buildCharacterId(region: string, realm: string, name: string): string {
  return `${region}-${realm}-${name.toLowerCase()}`;
}

export function createSeedCharacter(name: string, template: CharacterTemplate, region: string): SeedCharacterMeta {
  const character: Character = {
    id: buildCharacterId(region, TEST_REALM, name),
    region,
    realm: TEST_REALM,
    name,
    level: 80,
    classId: template.classId,
    raceId: template.raceId,
    portraitUrl: `https://example.test/portraits/${name.toLowerCase()}.jpg`,
    fetchedAt: "2026-03-18T12:00:00.000Z",
    specializations: template.specializations,
    activeSpecId: template.activeSpecId,
  };

  return {
    character,
    className: template.className,
    raceName: template.raceName,
  };
}

export function toStoredSelectedCharacter(character: Character, guild?: { id: number; name: string }) {
  return {
    id: character.id,
    region: character.region,
    realm: character.realm,
    name: character.name,
    portraitUrl: character.portraitUrl,
    fetchedAt: character.fetchedAt,
    profileSummary: {
      name: character.name,
      level: character.level,
      realm: {
        slug: character.realm,
        name: { en_US: TEST_REALM_NAME },
      },
      character_class: {
        id: character.classId,
        name: "",
      },
      race: {
        id: character.raceId,
        name: "",
      },
      ...(guild ? { guild: { id: guild.id, name: guild.name } } : {}),
    },
    mediaSummary: {
      assets: [{ key: "avatar", value: character.portraitUrl }],
    },
    specializationsSummary: {
      specializations: (character.specializations ?? []).map((spec) => ({
        specialization: { id: spec.id, name: spec.name },
      })),
      ...(character.activeSpecId != null
        ? {
            active_specialization: {
              id: character.activeSpecId,
              name: character.specializations?.find((spec) => spec.id === character.activeSpecId)?.name ?? "",
            },
          }
        : {}),
    },
  };
}

export function toAccountProfileSummary(characters: Character[]) {
  return {
    wow_accounts: [
      {
        id: 1,
        characters: characters.map((character, index) => ({
          id: index + 1,
          name: character.name,
          level: character.level,
          realm: {
            id: 1305,
            slug: character.realm,
            name: { en_US: TEST_REALM_NAME },
          },
          playable_class: { id: character.classId, name: "" },
          playable_race: { id: character.raceId, name: "" },
          faction: { type: "ALLIANCE", name: "Alliance" },
          gender: { type: "UNKNOWN", name: "Unknown" },
          protected_character: { href: `https://example.test/profile/wow/character/${character.realm}/${character.name.toLowerCase()}` },
        })),
      },
    ],
  };
}

export function getDocumentGuild(document: RaiderDocument): { id: number | null; name: string | null } {
  const selectedChar = document.characters.find(c => c.id === document.selectedCharacterId);
  const guild = selectedChar?.profileSummary?.guild as { id?: number; name?: string } | undefined;
  return {
    id: guild?.id ?? null,
    name: guild?.name ?? null,
  };
}

export function createRaiderDocument(
  battleNetId: string,
  guildName: string,
  guildId: number,
  characters: SeedCharacterMeta[],
  createdAt: string
): RaiderDocument {
  const guild = { id: guildId, name: guildName };
  return {
    id: battleNetId,
    battleNetId,
    selectedCharacterId: characters[0]?.character.id ?? null,
    createdAt,
    lastSeenAt: createdAt,
    characters: characters.map((entry) => toStoredSelectedCharacter(entry.character, guild)),
    accountProfileSummary: toAccountProfileSummary(characters.map((entry) => entry.character)),
    accountProfileFetchedAt: createdAt,
    accountProfileRefreshedAt: createdAt,
    // accountGuildsSummary intentionally omitted — guild comes from character profile
  };
}

export function createUnguildedRaiderDocument(
  battleNetId: string,
  characters: SeedCharacterMeta[],
  createdAt: string
): RaiderDocument {
  return {
    id: battleNetId,
    battleNetId,
    selectedCharacterId: characters[0]?.character.id ?? null,
    createdAt,
    lastSeenAt: createdAt,
    characters: characters.map((entry) => ({
      ...toStoredSelectedCharacter(entry.character),
      profileSummary: {
        ...toStoredSelectedCharacter(entry.character).profileSummary,
      },
    })),
    accountProfileSummary: toAccountProfileSummary(characters.map((entry) => entry.character)),
    accountProfileFetchedAt: createdAt,
    accountProfileRefreshedAt: createdAt,
  };
}

export function generateName(index: number): string {
  const prefix = NAME_PREFIXES[index % NAME_PREFIXES.length];
  const suffix = NAME_SUFFIXES[Math.floor(index / NAME_PREFIXES.length) % NAME_SUFFIXES.length];
  return `${prefix}${suffix}`;
}

export function buildRaiderSeeds(region: string, createdAt: string): { guild: RaiderSeed[]; outsider: RaiderSeed[] } {
  const guild: RaiderSeed[] = [];
  const outsider: RaiderSeed[] = [];

  const testCharacters = [
    createSeedCharacter("Aelrin", CHARACTER_TEMPLATES[1], region),
    createSeedCharacter("Brakka", CHARACTER_TEMPLATES[4], region),
  ];
  const guildMasterCharacters = [
    createSeedCharacter("Highlord", CHARACTER_TEMPLATES[1], region),
  ];
  const siteAdminCharacters = [
    createSeedCharacter("Observer", CHARACTER_TEMPLATES[5], region),
  ];
  const deleteAccountCharacters = [
    createSeedCharacter("Farewell", CHARACTER_TEMPLATES[5], region),
  ];
  guild.push({
    document: createRaiderDocument(
      TEST_MODE_IDENTITY.battleNetId,
      TEST_GUILD_NAME,
      TEST_MODE_IDENTITY.guildId ?? 12345,
      testCharacters,
      createdAt
    ),
    primary: testCharacters[0],
  });
  guild.push({
    document: createRaiderDocument(
      TEST_MODE_GUILD_MASTER_IDENTITY.battleNetId,
      TEST_GUILD_NAME,
      TEST_MODE_GUILD_MASTER_IDENTITY.guildId ?? 12345,
      guildMasterCharacters,
      createdAt
    ),
    primary: guildMasterCharacters[0],
  });
  guild.push({
    document: {
      ...createRaiderDocument(
        TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId,
        TEST_GUILD_NAME,
        TEST_MODE_NEEDS_CHARACTER_IDENTITY.guildId ?? 12345,
        testCharacters,
        createdAt
      ),
      selectedCharacterId: null,
    },
    primary: testCharacters[0],
  });
  guild.push({
    document: createUnguildedRaiderDocument(
      TEST_MODE_SITE_ADMIN_IDENTITY.battleNetId,
      siteAdminCharacters,
      createdAt
    ),
    primary: siteAdminCharacters[0],
  });
  guild.push({
    document: createRaiderDocument(
      TEST_MODE_DELETE_ACCOUNT_IDENTITY.battleNetId,
      TEST_GUILD_NAME,
      TEST_MODE_DELETE_ACCOUNT_IDENTITY.guildId ?? 12345,
      deleteAccountCharacters,
      createdAt
    ),
    primary: deleteAccountCharacters[0],
  });

  for (let index = 0; index < 31; index++) {
    const template = CHARACTER_TEMPLATES[index % CHARACTER_TEMPLATES.length];
    const name = generateName(index);
    const primary = createSeedCharacter(name, template, region);
    guild.push({
      document: createRaiderDocument(
        `guild-raider-${String(index + 1).padStart(2, "0")}`,
        TEST_GUILD_NAME,
        TEST_MODE_IDENTITY.guildId ?? 12345,
        [primary],
        createdAt
      ),
      primary,
    });
  }

  for (let index = 0; index < 14; index++) {
    const template = CHARACTER_TEMPLATES[(index + 5) % CHARACTER_TEMPLATES.length];
    const name = `Rival${generateName(index)}`;
    const primary = createSeedCharacter(name, template, region);
    outsider.push({
      document: createRaiderDocument(
        `outsider-raider-${String(index + 1).padStart(2, "0")}`,
        OUTSIDER_GUILD_NAME,
        OUTSIDER_GUILD_ID,
        [primary],
        createdAt
      ),
      primary,
    });
  }

  return { guild, outsider };
}

export function requireMode(instances: WowInstance[], instanceId: number, modeKey: string): { instance: WowInstance; players: number } {
  const instance = instances.find((entry) => entry.id === instanceId);
  if (!instance) {
    throw new Error(`Missing instance ${instanceId} in reference data`);
  }

  const mode = findModeByKey(instance, modeKey);
  if (!mode) {
    throw new Error(`Missing mode ${modeKey} for instance ${instance.name}`);
  }

  return { instance, players: getModePlayers(mode) };
}

export function buildRaidSignup(raidId: string, raider: RaiderSeed, index: number): RaidCharacter {
  const activeSpecId = raider.primary.character.activeSpecId ?? raider.primary.character.specializations?.[0]?.id ?? null;
  const activeSpec = raider.primary.character.specializations?.find((spec) => spec.id === activeSpecId)
    ?? raider.primary.character.specializations?.[0]
    ?? null;

  return {
    id: `${raidId}-signup-${raider.document.battleNetId}`,
    characterId: raider.primary.character.id,
    characterName: raider.primary.character.name,
    characterRealm: raider.primary.character.realm,
    characterLevel: raider.primary.character.level,
    characterClassId: raider.primary.character.classId,
    characterClassName: raider.primary.className,
    characterRaceId: raider.primary.character.raceId,
    characterRaceName: raider.primary.raceName,
    raiderBattleNetId: raider.document.battleNetId,
    desiredAttendance: ATTENDANCE_CYCLE[index % ATTENDANCE_CYCLE.length],
    reviewedAttendance: "IN",
    specId: activeSpec?.id ?? null,
    specName: activeSpec?.name ?? null,
    role: activeSpec?.role ?? null,
  };
}

export function buildRaidDocument(
  definition: RaidSeedDefinition,
  creator: RaiderSeed,
  signups: RaidCharacter[],
  now: Date,
  instances: WowInstance[]
): RaidDocument {
  const { instance } = requireMode(instances, definition.instanceId, definition.modeKey);
  const startTime = new Date(now.getTime() + definition.startHoursFromNow * 60 * 60 * 1000);
  const createdAt = new Date(now.getTime() - 48 * 60 * 60 * 1000);
  const expiryMs = startTime.getTime() + 7 * 24 * 3600 * 1000;
  const ttl = Math.max(86400, Math.floor((expiryMs - createdAt.getTime()) / 1000));
  return {
    id: definition.id,
    startTime: startTime.toISOString(),
    signupCloseTime: new Date(now.getTime() + definition.signupCloseHoursFromNow * 60 * 60 * 1000).toISOString(),
    description: definition.description,
    modeKey: definition.modeKey,
    visibility: definition.visibility,
    creatorGuild: getDocumentGuild(creator.document).name ?? "",
    creatorGuildId: getDocumentGuild(creator.document).id,
    instanceId: instance.id,
    instanceName: instance.name,
    creatorBattleNetId: creator.document.battleNetId,
    createdAt: createdAt.toISOString(),
    ttl,
    raidCharacters: signups,
  };
}

export function createRaidDefinitions(): RaidSeedDefinition[] {
  const definitions: RaidSeedDefinition[] = [
    {
      id: "raid-public-empty-deadmines",
      instanceId: 63,
      modeKey: "NORMAL:5",
      visibility: "PUBLIC",
      creatorBattleNetId: TEST_MODE_IDENTITY.battleNetId,
      description: "Public dungeon warmup",
      startHoursFromNow: 24,
      signupCloseHoursFromNow: 18,
      signupCount: 0,
      pool: "guild",
    },
    {
      id: "raid-public-signup-target-icc25",
      instanceId: 631,
      modeKey: "HEROIC:25",
      visibility: "PUBLIC",
      creatorBattleNetId: "guild-raider-01",
      description: "Heroic farm night",
      startHoursFromNow: 48,
      signupCloseHoursFromNow: 42,
      signupCount: 14,
      pool: "guild",
      poolOffset: 2,
    },
    {
      id: "raid-public-existing-signup-onyxia25",
      instanceId: 249,
      modeKey: "NORMAL:25",
      visibility: "PUBLIC",
      creatorBattleNetId: "guild-raider-02",
      description: "Dragon reset clear",
      startHoursFromNow: 54,
      signupCloseHoursFromNow: 46,
      signupCount: 12,
      pool: "guild",
      includeTestRaider: true,
      poolOffset: 4,
    },
    {
      id: "raid-guild-sparse-icc10",
      instanceId: 631,
      modeKey: "NORMAL:10",
      visibility: "GUILD",
      creatorBattleNetId: TEST_MODE_IDENTITY.battleNetId,
      description: "Guild ten-player alt run",
      startHoursFromNow: 72,
      signupCloseHoursFromNow: 64,
      signupCount: 5,
      pool: "guild",
      includeTestRaider: true,
      poolOffset: 6,
    },
    {
      id: "raid-guild-dense-molten-core",
      instanceId: 741,
      modeKey: "NORMAL:40",
      visibility: "GUILD",
      creatorBattleNetId: "guild-raider-03",
      description: "Guild retro forty-player night",
      startHoursFromNow: 96,
      signupCloseHoursFromNow: 88,
      signupCount: 30,
      pool: "guild",
      includeTestRaider: true,
      poolOffset: 1,
    },
    {
      id: "raid-public-closed-deadmines",
      instanceId: 63,
      modeKey: "HEROIC:5",
      visibility: "PUBLIC",
      creatorBattleNetId: "guild-raider-04",
      description: "Closed heroic cleanup",
      startHoursFromNow: 8,
      signupCloseHoursFromNow: -2,
      signupCount: 4,
      pool: "guild",
      poolOffset: 8,
    },
    {
      id: "raid-guild-closed-icc10",
      instanceId: 631,
      modeKey: "HEROIC:10",
      visibility: "GUILD",
      creatorBattleNetId: "guild-raider-05",
      description: "Closed progression lockout",
      startHoursFromNow: 12,
      signupCloseHoursFromNow: -3,
      signupCount: 8,
      pool: "guild",
      includeTestRaider: true,
      poolOffset: 10,
    },
    {
      id: "raid-outsider-guild-hidden",
      instanceId: 631,
      modeKey: "NORMAL:25",
      visibility: "GUILD",
      creatorBattleNetId: "outsider-raider-01",
      description: "Rival guild only raid",
      startHoursFromNow: 36,
      signupCloseHoursFromNow: 30,
      signupCount: 7,
      pool: "outsider",
      poolOffset: 0,
    },
  ];

  for (let index = 0; index < 12; index++) {
    definitions.push({
      id: `raid-public-generated-${String(index + 1).padStart(2, "0")}`,
      instanceId: [63, 249, 631, 741][index % 4],
      modeKey: ["NORMAL:5", "NORMAL:25", "HEROIC:10", "NORMAL:40"][index % 4],
      visibility: "PUBLIC",
      creatorBattleNetId: `guild-raider-${String((index % 10) + 6).padStart(2, "0")}`,
      description: `Public roster check ${index + 1}`,
      startHoursFromNow: 120 + index * 6,
      signupCloseHoursFromNow: 112 + index * 6,
      signupCount: [0, 3, 6, 12, 5, 8][index % 6],
      pool: "guild",
      includeTestRaider: index % 3 === 0,
      poolOffset: index,
    });
  }

  for (let index = 0; index < 12; index++) {
    definitions.push({
      id: `raid-guild-generated-${String(index + 1).padStart(2, "0")}`,
      instanceId: [631, 63, 741, 249][index % 4],
      modeKey: ["NORMAL:10", "HEROIC:5", "NORMAL:40", "NORMAL:25"][index % 4],
      visibility: "GUILD",
      creatorBattleNetId: index % 4 === 0
        ? TEST_MODE_IDENTITY.battleNetId
        : `guild-raider-${String((index % 12) + 10).padStart(2, "0")}`,
      description: `Guild calendar slot ${index + 1}`,
      startHoursFromNow: 216 + index * 6,
      signupCloseHoursFromNow: 206 + index * 6 - (index % 3 === 0 ? 12 : 0),
      signupCount: [2, 4, 8, 10, 16, 20][index % 6],
      pool: "guild",
      includeTestRaider: index % 2 === 0,
      poolOffset: index + 3,
    });
  }

  for (let index = 0; index < 8; index++) {
    definitions.push({
      id: `raid-outsider-generated-${String(index + 1).padStart(2, "0")}`,
      instanceId: [63, 249, 631, 741][index % 4],
      modeKey: ["NORMAL:5", "NORMAL:25", "HEROIC:25", "NORMAL:40"][index % 4],
      visibility: "GUILD",
      creatorBattleNetId: `outsider-raider-${String((index % 8) + 1).padStart(2, "0")}`,
      description: `Rival guild event ${index + 1}`,
      startHoursFromNow: 168 + index * 5,
      signupCloseHoursFromNow: 158 + index * 5,
      signupCount: [3, 5, 9, 14][index % 4],
      pool: "outsider",
      poolOffset: index,
    });
  }

  return definitions;
}

export function selectRaiders(pool: RaiderSeed[], count: number, offset = 0): RaiderSeed[] {
  if (pool.length === 0 || count <= 0) return [];
  const start = offset % pool.length;
  const ordered = [...pool.slice(start), ...pool.slice(0, start)];
  return ordered.slice(0, Math.min(count, pool.length));
}
