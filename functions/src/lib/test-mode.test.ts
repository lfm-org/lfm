import { describe, expect, it } from "vitest";
import {
  TEST_MODE_ACCESS_TOKEN,
  TEST_MODE_IDENTITY,
  getTestModeAccessTokenFromCookieHeader,
  getTestModeAccountCharacters,
  getTestModeIdentity,
  isLocalTestMode,
} from "./test-mode.js";

const enabledEnv = {
  TEST_MODE: "true",
  COSMOS_ENDPOINT: "http://localhost:8081",
};

describe("isLocalTestMode", () => {
  it("only enables test mode for TEST_MODE=true with an allowed local HTTP Cosmos endpoint", () => {
    expect(isLocalTestMode(enabledEnv)).toBe(true);
    expect(isLocalTestMode({ TEST_MODE: "true", COSMOS_ENDPOINT: "http://cosmosdb:8081" })).toBe(true);
    expect(isLocalTestMode({ TEST_MODE: "true", COSMOS_ENDPOINT: "https://localhost:8081" })).toBe(false);
    expect(isLocalTestMode({ TEST_MODE: "false", COSMOS_ENDPOINT: "http://localhost:8081" })).toBe(false);
    expect(isLocalTestMode({ TEST_MODE: "true", COSMOS_ENDPOINT: "http://example.test:8081" })).toBe(false);
  });
});

describe("getTestModeAccessTokenFromCookieHeader", () => {
  it("extracts the deterministic plain cookie only when local test mode is enabled", () => {
    expect(
      getTestModeAccessTokenFromCookieHeader(
        "foo=bar; battlenet_token=test_battlenet_token; hello=world",
        enabledEnv
      )
    ).toBe(TEST_MODE_ACCESS_TOKEN);

    expect(
      getTestModeAccessTokenFromCookieHeader("battlenet_token=test_battlenet_token", {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "https://localhost:8081",
      })
    ).toBeNull();

    expect(
      getTestModeAccessTokenFromCookieHeader("battlenet_token=wrong", enabledEnv)
    ).toBeNull();
  });
});

describe("getTestModeIdentity", () => {
  it("maps the deterministic token to the canonical identity only under the local test-mode guard", () => {
    expect(getTestModeIdentity(TEST_MODE_ACCESS_TOKEN, enabledEnv)).toEqual(TEST_MODE_IDENTITY);
    expect(getTestModeIdentity(TEST_MODE_ACCESS_TOKEN, {
      TEST_MODE: "true",
      COSMOS_ENDPOINT: "https://localhost:8081",
    })).toBeNull();
    expect(getTestModeIdentity("wrong-token", enabledEnv)).toBeNull();
  });
});

describe("getTestModeAccountCharacters", () => {
  it("returns a deterministic non-empty account-character list only under the local test-mode guard", () => {
    expect(getTestModeAccountCharacters(TEST_MODE_ACCESS_TOKEN, "eu", enabledEnv)).toEqual([
      {
        name: "Aelrin",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
      },
      {
        name: "Brakka",
        realm: "test-realm",
        realmName: "Test Realm",
        level: 80,
        region: "eu",
      },
    ]);

    expect(getTestModeAccountCharacters(TEST_MODE_ACCESS_TOKEN, "eu", {
      TEST_MODE: "true",
      COSMOS_ENDPOINT: "https://localhost:8081",
    })).toBeNull();
  });
});
