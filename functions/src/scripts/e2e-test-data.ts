import { existsSync } from "fs";
import { readFile } from "fs/promises";
import path from "path";
import { fileURLToPath } from "url";
import {
  TEST_MODE_IDENTITY,
  TEST_MODE_NEEDS_CHARACTER_IDENTITY,
  isLocalTestMode,
} from "../lib/test-mode.js";
import type {
  EntitySyncMeta,
  GuildDocument,
  RaiderDocument,
  RaidCharacter,
  RaidDocument,
  WowClass,
  WowInstance,
  WowRace,
  WowSpecialization,
} from "../types/index.js";
import {
  buildRaiderSeeds,
  buildRaidSignup,
  buildRaidDocument,
  createRaidDefinitions,
  requireMode,
  selectRaiders,
  type SeedOptions,
} from "./e2e-seed-builders.js";

export interface ReferenceDataBundle {
  classes: WowClass[];
  races: WowRace[];
  specializations: WowSpecialization[];
  instances: WowInstance[];
}

export interface BlobWrite {
  blobName: string;
  data: unknown;
}

export interface SeedDataBundle {
  raiders: RaiderDocument[];
  raids: RaidDocument[];
  guilds: GuildDocument[];
}

export type E2eScenario =
  | "default"
  | "raids-empty"
  | "raids-error"
  | "characters-empty"
  | "instances-missing";

export function resolveTestDataTimestamp(
  explicitTimestamp?: string | null,
  now: Date = new Date()
): string {
  return explicitTimestamp || now.toISOString();
}

export function resolveE2eScenario(value?: string | null): E2eScenario {
  switch (value) {
    case "raids-empty":
    case "raids-error":
    case "characters-empty":
    case "instances-missing":
      return value;
    default:
      return "default";
  }
}

export function assertLocalSeedEnvironment(env: Record<string, string | undefined> = process.env): void {
  if (!isLocalTestMode(env)) {
    throw new Error("seed-test-data only supports local TEST_MODE with an allowed local HTTP Cosmos endpoint");
  }
}

export interface RaidSeedDefinition {
  id: string;
  instanceId: number;
  modeKey: string;
  visibility: "PUBLIC" | "GUILD";
  creatorBattleNetId: string;
  description: string;
  startHoursFromNow: number;
  signupCloseHoursFromNow: number;
  signupCount: number;
  pool: "guild" | "outsider";
  includeTestRaider?: boolean;
  poolOffset?: number;
}

const TEST_REALM = "test-realm";
const TEST_REALM_NAME = "Test Realm";
const STALE_GUILD_ID = 65432;
const STALE_GUILD_NAME = "Stale Vanguard";

function createMeta(timestamp: string): EntitySyncMeta {
  return {
    lastSuccessTime: timestamp,
    lastFailureTime: null,
    lastFailureReason: null,
  };
}

function createIndexLinks(entity: string) {
  return {
    self: { href: `https://example.test/data/wow/${entity}/index` },
  };
}

function toReferenceRoleType(role: WowSpecialization["role"]): "DAMAGE" | "HEALER" | "TANK" {
  return role === "DPS" ? "DAMAGE" : role;
}

function getSnapshotCandidates(): string[] {
  const scriptDir = path.dirname(fileURLToPath(import.meta.url));
  return [
    path.resolve(process.cwd(), "functions/test-data/wow"),
    path.resolve(process.cwd(), "test-data/wow"),
    path.resolve(scriptDir, "../../../test-data/wow"),
  ];
}

export function resolveSnapshotDir(): string {
  return getSnapshotCandidates().find((candidate) => existsSync(candidate)) ?? getSnapshotCandidates()[0];
}

async function readJsonFile<T>(filePath: string): Promise<T> {
  const raw = await readFile(filePath, "utf8");
  return JSON.parse(raw) as T;
}

export async function loadReferenceDataBundle(snapshotDir = resolveSnapshotDir()): Promise<ReferenceDataBundle> {
  const [classes, races, specializations, instances] = await Promise.all([
    readJsonFile<WowClass[]>(path.join(snapshotDir, "classes.json")),
    readJsonFile<WowRace[]>(path.join(snapshotDir, "races.json")),
    readJsonFile<WowSpecialization[]>(path.join(snapshotDir, "specializations.json")),
    readJsonFile<WowInstance[]>(path.join(snapshotDir, "instances.json")),
  ]);

  return { classes, races, specializations, instances };
}

export function buildReferenceDataWrites(
  bundle: ReferenceDataBundle,
  timestamp: string,
  scenario: E2eScenario = "default"
): BlobWrite[] {
  const classesById = new Map(bundle.classes.map((entry) => [entry.id, entry]));

  const writes: BlobWrite[] = [
    {
      blobName: "reference/playable-class/index.json",
      data: {
        _links: createIndexLinks("playable-class"),
        classes: bundle.classes.map((entry) => ({
          key: { href: `https://example.test/data/wow/playable-class/${entry.id}` },
          id: entry.id,
          name: entry.name,
        })),
      },
    },
    { blobName: "reference/playable-class/meta.json", data: createMeta(timestamp) },
    ...bundle.classes.map((entry) => ({
      blobName: `reference/playable-class/${entry.id}.json`,
      data: {
        id: entry.id,
        name: entry.name,
      },
    })),
    {
      blobName: "reference/playable-race/index.json",
      data: {
        _links: createIndexLinks("playable-race"),
        races: bundle.races.map((entry) => ({
          key: { href: `https://example.test/data/wow/playable-race/${entry.id}` },
          id: entry.id,
          name: entry.name,
        })),
      },
    },
    { blobName: "reference/playable-race/meta.json", data: createMeta(timestamp) },
    ...bundle.races.map((entry) => ({
      blobName: `reference/playable-race/${entry.id}.json`,
      data: {
        id: entry.id,
        name: entry.name,
        faction: { type: entry.faction, name: entry.faction },
      },
    })),
    {
      blobName: "reference/playable-specialization/index.json",
      data: {
        _links: createIndexLinks("playable-specialization"),
        character_specializations: bundle.specializations.map((entry) => ({
          key: { href: `https://example.test/data/wow/playable-specialization/${entry.id}` },
          id: entry.id,
          name: entry.name,
        })),
      },
    },
    { blobName: "reference/playable-specialization/meta.json", data: createMeta(timestamp) },
    ...bundle.specializations.map((entry) => ({
      blobName: `reference/playable-specialization/${entry.id}.json`,
      data: {
        id: entry.id,
        name: entry.name,
        playable_class: {
          id: entry.classId,
          name: classesById.get(entry.classId)?.name ?? "",
        },
        role: {
          type: toReferenceRoleType(entry.role),
          name: entry.role,
        },
      },
    })),
  ];

  if (scenario !== "instances-missing") {
    writes.push(
      {
        blobName: "reference/journal-instance/index.json",
        data: {
          _links: createIndexLinks("journal-instance"),
          instances: bundle.instances.map((entry) => ({
            key: { href: `https://example.test/data/wow/journal-instance/${entry.id}` },
            id: entry.id,
            name: { en_US: entry.name },
          })),
        },
      },
      { blobName: "reference/journal-instance/meta.json", data: createMeta(timestamp) },
      ...bundle.instances.map((entry) => ({
        blobName: `reference/journal-instance/${entry.id}.json`,
        data: {
          id: entry.id,
          name: entry.name,
          category: { type: entry.type },
          expansion: { id: entry.expansionId, name: "" },
          minimum_level: entry.minLevel,
          modes: entry.modes,
        },
      }))
    );
  }

  return writes;
}

export function buildSeedData({
  now,
  region,
  instances,
  raidDefinitions,
  scenario = "default",
}: SeedOptions): SeedDataBundle {
  const seedTime = new Date(now);
  const createdAt = new Date(seedTime.getTime() - 72 * 60 * 60 * 1000).toISOString();
  const raiders = buildRaiderSeeds(region, createdAt);
  const guildPool = raiders.guild;
  const outsiderPool = raiders.outsider;

  if (scenario === "characters-empty") {
    guildPool[0] = {
      ...guildPool[0],
      document: {
        ...guildPool[0].document,
        selectedCharacterId: null,
        accountProfileSummary: { wow_accounts: [{ id: 1, characters: [] }] },
        accountProfileFetchedAt: createdAt,
        accountProfileRefreshedAt: createdAt,
      },
    };
  }

  const definitions = raidDefinitions ?? createRaidDefinitions();
  const localTestBattleNetIds = new Set([
    TEST_MODE_IDENTITY.battleNetId,
    TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId,
  ]);
  const raids = scenario === "raids-empty" ? [] : definitions.map((definition) => {
    const sourcePool = definition.pool === "guild" ? guildPool : outsiderPool;
    const { players } = requireMode(instances, definition.instanceId, definition.modeKey);
    const creator = sourcePool.find((raider) => raider.document.battleNetId === definition.creatorBattleNetId);
    if (!creator) {
      throw new Error(`Missing creator ${definition.creatorBattleNetId} for raid seed ${definition.id}`);
    }

    const signups: RaidCharacter[] = [];
    const requestedCount = Math.min(definition.signupCount, players);
    const availablePool = sourcePool.filter(
      (raider) => !localTestBattleNetIds.has(raider.document.battleNetId)
    );
    const availableSignups = availablePool.length + (definition.includeTestRaider && definition.pool === "guild" ? 1 : 0);
    if (requestedCount > availableSignups) {
      throw new Error(`Not enough ${definition.pool} raiders to seed ${requestedCount} signups for ${definition.id}`);
    }

    if (definition.includeTestRaider && definition.pool === "guild") {
      signups.push(buildRaidSignup(definition.id, guildPool[0], 0));
    }

    const remainingCount = Math.max(0, requestedCount - signups.length);
    const selected = selectRaiders(
      availablePool,
      remainingCount,
      definition.poolOffset ?? 0
    );

    const attendanceOffset = signups.length;
    selected.forEach((raider, index) => {
      signups.push(buildRaidSignup(definition.id, raider, attendanceOffset + index));
    });

    return buildRaidDocument(definition, creator, signups, seedTime, instances);
  });

  const staleFetchedAt = new Date(seedTime.getTime() - 2 * 60 * 60 * 1000).toISOString();
  const guilds: GuildDocument[] = [
    {
      id: String(STALE_GUILD_ID),
      guildId: STALE_GUILD_ID,
      realmSlug: TEST_REALM,
      slogan: "Hold roster until sync returns.",
      blizzardProfileRaw: {
        id: STALE_GUILD_ID,
        name: STALE_GUILD_NAME,
        achievement_points: 2400,
        member_count: 42,
        realm: {
          id: 559,
          slug: TEST_REALM,
          name: { en_US: TEST_REALM_NAME },
        },
        faction: { type: "ALLIANCE", name: "Alliance" },
      },
      blizzardProfileFetchedAt: staleFetchedAt,
      blizzardRosterRaw: {
        guild: {
          id: STALE_GUILD_ID,
          name: STALE_GUILD_NAME,
          realm: {
            id: 559,
            slug: TEST_REALM,
            name: { en_US: TEST_REALM_NAME },
          },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        members: [
          {
            character: {
              id: 91001,
              name: "Archivist",
              realm: {
                id: 559,
                slug: TEST_REALM,
                name: { en_US: TEST_REALM_NAME },
              },
              level: 80,
              playable_class: { id: 8 },
              playable_race: { id: 7 },
              faction: { type: "ALLIANCE", name: "Alliance" },
            },
            rank: 0,
          },
          {
            character: {
              id: 91002,
              name: "Quarterline",
              realm: {
                id: 559,
                slug: TEST_REALM,
                name: { en_US: TEST_REALM_NAME },
              },
              level: 80,
              playable_class: { id: 2 },
              playable_race: { id: 3 },
              faction: { type: "ALLIANCE", name: "Alliance" },
            },
            rank: 2,
          },
        ],
      },
      blizzardRosterFetchedAt: staleFetchedAt,
      rankPermissions: [
        { rank: 0, canCreateGuildRaids: true, canSignupGuildRaids: true },
        { rank: 2, canCreateGuildRaids: false, canSignupGuildRaids: true },
      ],
      setup: {
        initializedAt: createdAt,
        timezone: "UTC",
      },
    },
  ];

  return {
    raiders: [...guildPool.map((entry) => entry.document), ...outsiderPool.map((entry) => entry.document)],
    raids,
    guilds,
  };
}
