import type { AccountCharacter, BattleNetIdentity } from "../types/index.js";

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

export function getTestModeAccountCharacters(
  accessToken: string,
  region: string,
  env: EnvLike = process.env
): AccountCharacter[] | null {
  if (!getTestModeIdentity(accessToken, env)) return null;

  return [
    { name: "Aelrin", realm: "test-realm", realmName: "Test Realm", level: 80, region },
    { name: "Brakka", realm: "test-realm", realmName: "Test Realm", level: 80, region },
  ];
}
