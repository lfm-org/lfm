import { afterEach, describe, expect, it, vi } from "vitest";
import { BattlenetService } from "./battlenet.js";
import { TEST_MODE_IDENTITY } from "./test-mode.js";

const originalEnv = { ...process.env };
const originalFetch = global.fetch;

afterEach(() => {
  process.env = { ...originalEnv };
  global.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe("BattlenetService local test mode", () => {
  it("resolveIdentity short-circuits to the canonical identity without calling fetch", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";

    const fetchSpy = vi.fn(() => {
      throw new Error("fetch should not be called");
    });
    global.fetch = fetchSpy as typeof fetch;

    const service = new BattlenetService();
    await expect(service.resolveIdentity("test_battlenet_token")).resolves.toEqual(TEST_MODE_IDENTITY);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("fetchAccountCharacters short-circuits to deterministic data without calling fetch", async () => {
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";
    process.env.BATTLE_NET_REGION = "eu";

    const fetchSpy = vi.fn(() => {
      throw new Error("fetch should not be called");
    });
    global.fetch = fetchSpy as typeof fetch;

    const service = new BattlenetService();
    await expect(service.fetchAccountCharacters("test_battlenet_token")).resolves.toEqual([
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
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
