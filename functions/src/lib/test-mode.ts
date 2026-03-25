import { toAccountCharacterViews } from "./blizzard-adapters.js";
import type { BattleNetIdentity } from "../types/index.js";
import type {
  BlizzardAccountProfileSummary,
  BlizzardGuildProfileResponse,
  BlizzardGuildRosterResponse,
  BlizzardMediaSummary,
  BlizzardUserInfo,
} from "../types/blizzard.js";

type EnvLike = Record<string, string | undefined>;
export type TestModeAuthScenario =
  | "default"
  | "needs-character"
  | "guild-master"
  | "site-admin"
  | "delete-account";

const ALLOWED_LOCAL_TEST_HOSTNAMES = new Set([
  "localhost",
  "127.0.0.1",
  "::1",
  "cosmosdb",
  "host.docker.internal",
]);

export const TEST_MODE_ACCESS_TOKEN = "test_battlenet_token";
export const TEST_MODE_CALLBACK_CODE = "test-battlenet-code";
export const TEST_MODE_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id",
  guildId: 12345,
  guildName: "Test Guild",
};
export const TEST_MODE_NEEDS_CHARACTER_ACCESS_TOKEN = "test_battlenet_token_needs_character";
export const TEST_MODE_NEEDS_CHARACTER_CALLBACK_CODE = "test-battlenet-code-needs-character";
export const TEST_MODE_NEEDS_CHARACTER_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id-needs-character",
  guildId: 12345,
  guildName: "Test Guild",
};
export const TEST_MODE_GUILD_MASTER_ACCESS_TOKEN = "test_battlenet_token_guild_master";
export const TEST_MODE_GUILD_MASTER_CALLBACK_CODE = "test-battlenet-code-guild-master";
export const TEST_MODE_GUILD_MASTER_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id-guild-master",
  guildId: 12345,
  guildName: "Test Guild",
};
export const TEST_MODE_SITE_ADMIN_ACCESS_TOKEN = "test_battlenet_token_site_admin";
export const TEST_MODE_SITE_ADMIN_CALLBACK_CODE = "test-battlenet-code-site-admin";
export const TEST_MODE_SITE_ADMIN_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id-admin",
  guildId: null,
  guildName: null,
};
export const TEST_MODE_DELETE_ACCOUNT_ACCESS_TOKEN = "test_battlenet_token_delete_account";
export const TEST_MODE_DELETE_ACCOUNT_CALLBACK_CODE = "test-battlenet-code-delete-account";
export const TEST_MODE_DELETE_ACCOUNT_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id-delete-account",
  guildId: 12345,
  guildName: "Test Guild",
};

interface TestModeAuthFixture {
  accessToken: string;
  callbackCode: string;
  identity: BattleNetIdentity;
}

const TEST_MODE_AUTH_FIXTURES: Record<TestModeAuthScenario, TestModeAuthFixture> = {
  default: {
    accessToken: TEST_MODE_ACCESS_TOKEN,
    callbackCode: TEST_MODE_CALLBACK_CODE,
    identity: TEST_MODE_IDENTITY,
  },
  "needs-character": {
    accessToken: TEST_MODE_NEEDS_CHARACTER_ACCESS_TOKEN,
    callbackCode: TEST_MODE_NEEDS_CHARACTER_CALLBACK_CODE,
    identity: TEST_MODE_NEEDS_CHARACTER_IDENTITY,
  },
  "guild-master": {
    accessToken: TEST_MODE_GUILD_MASTER_ACCESS_TOKEN,
    callbackCode: TEST_MODE_GUILD_MASTER_CALLBACK_CODE,
    identity: TEST_MODE_GUILD_MASTER_IDENTITY,
  },
  "site-admin": {
    accessToken: TEST_MODE_SITE_ADMIN_ACCESS_TOKEN,
    callbackCode: TEST_MODE_SITE_ADMIN_CALLBACK_CODE,
    identity: TEST_MODE_SITE_ADMIN_IDENTITY,
  },
  "delete-account": {
    accessToken: TEST_MODE_DELETE_ACCOUNT_ACCESS_TOKEN,
    callbackCode: TEST_MODE_DELETE_ACCOUNT_CALLBACK_CODE,
    identity: TEST_MODE_DELETE_ACCOUNT_IDENTITY,
  },
};

function buildGuildCrestDataUrl(label: string, color: string): string {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="20" fill="transparent"/><circle cx="64" cy="64" r="42" fill="${color}" opacity="0.35"/><text x="64" y="76" text-anchor="middle" font-size="54" fill="${color}" font-family="sans-serif">${label}</text></svg>`;
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;
}

function readCookie(cookieHeader: string, name: string): string | null {
  const match = cookieHeader.match(new RegExp(`(?:^|;\\s*)${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

function getTestModeFixtureByAccessToken(accessToken: string): TestModeAuthFixture | null {
  return Object.values(TEST_MODE_AUTH_FIXTURES).find((fixture) => fixture.accessToken === accessToken) ?? null;
}

function getTestModeFixtureByCallbackCode(callbackCode: string): TestModeAuthFixture | null {
  return Object.values(TEST_MODE_AUTH_FIXTURES).find((fixture) => fixture.callbackCode === callbackCode) ?? null;
}

export function normalizeTestModeAuthScenario(value?: string | null): TestModeAuthScenario {
  if (value === "needs-character") return "needs-character";
  if (value === "guild-master") return "guild-master";
  if (value === "site-admin") return "site-admin";
  if (value === "delete-account") return "delete-account";
  return "default";
}

export function isLocalTestMode(env: EnvLike = process.env): boolean {
  const endpoint = env.COSMOS_ENDPOINT ?? "";
  if (env.TEST_MODE !== "true" || !endpoint.startsWith("http://")) return false;

  try {
    return ALLOWED_LOCAL_TEST_HOSTNAMES.has(new URL(endpoint).hostname.toLowerCase());
  } catch {
    return false;
  }
}

export function getTestModeAccessTokenFromCookieHeader(cookieHeader: string, env: EnvLike = process.env): string | null {
  if (!isLocalTestMode(env)) return null;

  const token = readCookie(cookieHeader, "battlenet_token");
  return token && getTestModeFixtureByAccessToken(token) ? token : null;
}

export function getTestModeAccessTokenForCallbackCode(
  callbackCode: string,
  env: EnvLike = process.env
): string | null {
  if (!isLocalTestMode(env)) return null;
  return getTestModeFixtureByCallbackCode(callbackCode)?.accessToken ?? null;
}

export function getTestModeCallbackCodeForScenario(
  scenario?: string | null,
  env: EnvLike = process.env
): string | null {
  if (!isLocalTestMode(env)) return null;
  return TEST_MODE_AUTH_FIXTURES[normalizeTestModeAuthScenario(scenario)].callbackCode;
}

export function getTestModeIdentity(accessToken: string, env: EnvLike = process.env): BattleNetIdentity | null {
  if (!isLocalTestMode(env)) return null;
  return getTestModeFixtureByAccessToken(accessToken)?.identity ?? null;
}

export function getTestModeUserInfo(
  accessToken: string,
  env: EnvLike = process.env
): BlizzardUserInfo | null {
  const identity = getTestModeIdentity(accessToken, env);
  if (!identity) return null;

  return {
    id: identity.battleNetId === TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId
      ? 2
      : identity.battleNetId === TEST_MODE_GUILD_MASTER_IDENTITY.battleNetId
        ? 3
        : identity.battleNetId === TEST_MODE_SITE_ADMIN_IDENTITY.battleNetId
          ? 4
          : identity.battleNetId === TEST_MODE_DELETE_ACCOUNT_IDENTITY.battleNetId
            ? 5
          : 1,
    battletag: "Test#1234",
  };
}

export function getTestModeAccountProfileSummary(
  accessToken: string,
  env: EnvLike = process.env
): BlizzardAccountProfileSummary | null {
  if (!getTestModeIdentity(accessToken, env)) return null;

  if (accessToken === TEST_MODE_GUILD_MASTER_ACCESS_TOKEN) {
    return {
      wow_accounts: [
        {
          id: 3,
          characters: [
            {
              id: 201,
              name: "Highlord",
              level: 80,
              realm: {
                id: 1305,
                slug: "test-realm",
                name: { en_US: "Test Realm" },
              },
              playable_class: { id: 2, name: "Paladin" },
              playable_race: { id: 3, name: "Dwarf" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "MALE", name: "Male" },
              guild: { id: 12345, name: "Test Guild" },
              protected_character: { href: "https://example.test/profile/wow/character/test-realm/highlord" },
            },
          ],
        },
      ],
    };
  }

  if (accessToken === TEST_MODE_SITE_ADMIN_ACCESS_TOKEN) {
    return {
      wow_accounts: [
        {
          id: 4,
          characters: [
            {
              id: 301,
              name: "Observer",
              level: 80,
              realm: {
                id: 1305,
                slug: "test-realm",
                name: { en_US: "Test Realm" },
              },
              playable_class: { id: 8, name: "Mage" },
              playable_race: { id: 7, name: "Gnome" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "MALE", name: "Male" },
              protected_character: { href: "https://example.test/profile/wow/character/test-realm/observer" },
            },
          ],
        },
      ],
    };
  }

  if (accessToken === TEST_MODE_DELETE_ACCOUNT_ACCESS_TOKEN) {
    return {
      wow_accounts: [
        {
          id: 5,
          characters: [
            {
              id: 401,
              name: "Farewell",
              level: 80,
              realm: {
                id: 1305,
                slug: "test-realm",
                name: { en_US: "Test Realm" },
              },
              playable_class: { id: 8, name: "Mage" },
              playable_race: { id: 7, name: "Gnome" },
              faction: { type: "ALLIANCE", name: "Alliance" },
              gender: { type: "FEMALE", name: "Female" },
              guild: { id: 12345, name: "Test Guild" },
              protected_character: { href: "https://example.test/profile/wow/character/test-realm/farewell" },
            },
          ],
        },
      ],
    };
  }

  return {
    wow_accounts: [
      {
        id: 1,
        characters: [
          {
            id: 101,
            name: "Aelrin",
            level: 80,
            realm: {
              id: 1305,
              slug: "test-realm",
              name: { en_US: "Test Realm" },
            },
            playable_class: { id: 2, name: "Paladin" },
            playable_race: { id: 11, name: "Draenei" },
            faction: { type: "ALLIANCE", name: "Alliance" },
            gender: { type: "FEMALE", name: "Female" },
            guild: { id: 12345, name: "Test Guild" },
            protected_character: { href: "https://example.test/profile/wow/character/test-realm/aelrin" },
          },
          {
            id: 102,
            name: "Brakka",
            level: 80,
            realm: {
              id: 1305,
              slug: "test-realm",
              name: { en_US: "Test Realm" },
            },
            playable_class: { id: 1, name: "Warrior" },
            playable_race: { id: 2, name: "Orc" },
            faction: { type: "HORDE", name: "Horde" },
            gender: { type: "MALE", name: "Male" },
            guild: { id: 12345, name: "Test Guild" },
            protected_character: { href: "https://example.test/profile/wow/character/test-realm/brakka" },
          },
        ],
      },
    ],
  };
}

export function getTestModeAccountCharacters(
  accessToken: string,
  region: string,
  env: EnvLike = process.env
) {
  const summary = getTestModeAccountProfileSummary(accessToken, env);
  return summary ? toAccountCharacterViews(summary, region) : null;
}

export function getTestModeGuildProfile(
  accessToken: string,
  realmSlug: string,
  guildNameSlug: string,
  env: EnvLike = process.env
): BlizzardGuildProfileResponse | null {
  const identity = getTestModeIdentity(accessToken, env);
  if (!identity) return null;

  const guildId = guildNameSlug === "rival-guild" ? 54321 : identity.guildId ?? 12345;
  const guildName = guildNameSlug === "rival-guild" ? "Rival Guild" : identity.guildName ?? "Test Guild";

  return {
    id: guildId,
    name: guildName,
    achievement_points: 1985,
    member_count: 369,
    realm: {
      id: 559,
      slug: realmSlug,
      name: { en_US: "Test Realm" },
    },
    motd: "Practice clean pulls and cleaner logs.",
    faction: { type: "ALLIANCE", name: "Alliance" },
    crest: {
      emblem: {
        id: 50,
        media: {
          key: {
            href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/emblem/50?namespace=static-eu",
          },
          id: 50,
        },
        color: { id: 16, rgba: { r: 223, g: 165, b: 90, a: 1 } },
      },
      border: {
        id: 1,
        media: {
          key: {
            href: "https://eu.api.blizzard.com/data/wow/media/guild-crest/border/1?namespace=static-eu",
          },
          id: 1,
        },
        color: { id: 16, rgba: { r: 249, g: 204, b: 48, a: 1 } },
      },
      background: {
        color: { id: 2, rgba: { r: 158, g: 0, b: 54, a: 1 } },
      },
    },
  };
}

export function getTestModeGuildRoster(
  accessToken: string,
  realmSlug: string,
  guildNameSlug: string,
  env: EnvLike = process.env
): BlizzardGuildRosterResponse | null {
  const identity = getTestModeIdentity(accessToken, env);
  if (!identity) return null;

  const guildId = guildNameSlug === "rival-guild" ? 54321 : identity.guildId ?? 12345;
  const guildName = guildNameSlug === "rival-guild" ? "Rival Guild" : identity.guildName ?? "Test Guild";

  return {
    guild: {
      name: guildName,
      id: guildId,
      realm: {
        id: 559,
        slug: realmSlug,
        name: { en_US: "Test Realm" },
      },
      faction: { type: "ALLIANCE", name: "Alliance" },
    },
    members: [
      {
        character: {
          name: "Highlord",
          id: 201,
          realm: { id: 559, slug: realmSlug, name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 2 },
          playable_race: { id: 3 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 0,
      },
      {
        character: {
          name: "Aelrin",
          id: 101,
          realm: { id: 559, slug: realmSlug, name: { en_US: "Test Realm" } },
          level: 80,
          playable_class: { id: 2 },
          playable_race: { id: 11 },
          faction: { type: "ALLIANCE", name: "Alliance" },
        },
        rank: 2,
      },
      {
        character: {
          name: "Brakka",
          id: 102,
          realm: { id: 559, slug: realmSlug, name: { en_US: "Test Realm" } },
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

export function getTestModeGuildCrestMedia(
  accessToken: string,
  href: string,
  env: EnvLike = process.env
): BlizzardMediaSummary | null {
  if (!getTestModeIdentity(accessToken, env)) return null;

  if (href.includes("/guild-crest/emblem/")) {
    return {
      assets: [
        { key: "icon", value: buildGuildCrestDataUrl("E", "#dfa55a") },
      ],
    };
  }

  if (href.includes("/guild-crest/border/")) {
    return {
      assets: [
        { key: "icon", value: buildGuildCrestDataUrl("B", "#f9cc30") },
      ],
    };
  }

  return null;
}
