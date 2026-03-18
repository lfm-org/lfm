import type { AccountCharacter, BattleNetIdentity } from "../types/index.js";

type EnvLike = Record<string, string | undefined>;

const ALLOWED_LOCAL_TEST_HOSTNAMES = new Set([
  "localhost",
  "127.0.0.1",
  "::1",
  "cosmosdb",
  "host.docker.internal",
]);

export const TEST_MODE_ACCESS_TOKEN = "test_battlenet_token";
export const TEST_MODE_IDENTITY: BattleNetIdentity = {
  battleNetId: "test-bnet-id",
  guildId: 12345,
  guildName: "Test Guild",
};

function readCookie(cookieHeader: string, name: string): string | null {
  const match = cookieHeader.match(new RegExp(`(?:^|;\\s*)${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
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
  return token === TEST_MODE_ACCESS_TOKEN ? TEST_MODE_ACCESS_TOKEN : null;
}

export function getTestModeIdentity(accessToken: string, env: EnvLike = process.env): BattleNetIdentity | null {
  if (!isLocalTestMode(env) || accessToken !== TEST_MODE_ACCESS_TOKEN) return null;
  return TEST_MODE_IDENTITY;
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
