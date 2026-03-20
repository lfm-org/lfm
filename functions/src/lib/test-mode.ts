import { toAccountCharacterViews } from "./blizzard-adapters.js";
import type { BattleNetIdentity } from "../types/index.js";
import type {
  BlizzardAccountGuildsSummary,
  BlizzardAccountProfileSummary,
  BlizzardUserInfo,
} from "../types/blizzard.js";

type EnvLike = Record<string, string | undefined>;
export type TestModeAuthScenario = "default" | "needs-character";

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
};

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
  return value === "needs-character" ? "needs-character" : "default";
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
    id: identity.battleNetId === TEST_MODE_NEEDS_CHARACTER_IDENTITY.battleNetId ? 2 : 1,
    battletag: "Test#1234",
  };
}

export function getTestModeAccountGuildsSummary(
  accessToken: string,
  env: EnvLike = process.env
): BlizzardAccountGuildsSummary | null {
  const identity = getTestModeIdentity(accessToken, env);
  if (!identity) return null;

  return {
    guilds: identity.guildId && identity.guildName
      ? [{ guild: { id: identity.guildId, name: identity.guildName } }]
      : [],
  };
}

export function getTestModeAccountProfileSummary(
  accessToken: string,
  env: EnvLike = process.env
): BlizzardAccountProfileSummary | null {
  if (!getTestModeIdentity(accessToken, env)) return null;

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
