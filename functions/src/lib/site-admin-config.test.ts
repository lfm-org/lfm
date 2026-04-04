import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TEST_MODE_SITE_ADMIN_IDENTITY } from "./test-mode.js";

const getSecret = vi.fn();
const SecretClient = vi.fn(function SecretClient() {
  return { getSecret };
});
const DefaultAzureCredential = vi.fn(function DefaultAzureCredential() {});

vi.mock("@azure/keyvault-secrets", () => ({
  SecretClient,
}));

vi.mock("@azure/identity", () => ({
  DefaultAzureCredential,
}));

describe("site-admin-config", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
    vi.useFakeTimers();
    process.env.KEY_VAULT_URL = "https://myapp-kv.vault.azure.net/";
  });

  afterEach(() => {
    vi.useRealTimers();
    delete process.env.KEY_VAULT_URL;
    delete process.env.TEST_MODE;
    delete process.env.COSMOS_ENDPOINT;
  });

  it("parses comma and newline separated hashed ids from Key Vault", async () => {
    getSecret.mockResolvedValue({ value: "alpha,\n beta\n\nalpha" });

    const { isSiteAdmin, resetSiteAdminCacheForTests } = await import("./site-admin-config.js");
    resetSiteAdminCacheForTests();

    await expect(isSiteAdmin("alpha")).resolves.toBe(true);
    await expect(isSiteAdmin("beta")).resolves.toBe(true);
    await expect(isSiteAdmin("gamma")).resolves.toBe(false);
    expect(getSecret).toHaveBeenCalledWith("site-admin-battle-net-ids");
  });

  it("allows the local test-mode site-admin identity without consulting Key Vault", async () => {
    const { isSiteAdmin, resetSiteAdminCacheForTests } = await import("./site-admin-config.js");
    resetSiteAdminCacheForTests();

    await expect(
      isSiteAdmin(TEST_MODE_SITE_ADMIN_IDENTITY.battleNetId, {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://localhost:8081",
        KEY_VAULT_URL: undefined,
      }),
    ).resolves.toBe(true);
    expect(getSecret).not.toHaveBeenCalled();
  });

  it("uses the cached allowlist until the ttl expires", async () => {
    getSecret
      .mockResolvedValueOnce({ value: "alpha" })
      .mockResolvedValueOnce({ value: "beta" });

    const { CACHE_TTL_MS, isSiteAdmin, resetSiteAdminCacheForTests } = await import("./site-admin-config.js");
    resetSiteAdminCacheForTests();

    await expect(isSiteAdmin("alpha")).resolves.toBe(true);
    await expect(isSiteAdmin("alpha")).resolves.toBe(true);

    vi.advanceTimersByTime(CACHE_TTL_MS + 1);
    await expect(isSiteAdmin("beta")).resolves.toBe(true);
    expect(getSecret).toHaveBeenCalledTimes(2);
  });

  it("serves the stale cache when the Key Vault refresh fails", async () => {
    getSecret
      .mockResolvedValueOnce({ value: "alpha" })
      .mockRejectedValueOnce(new Error("kv down"));

    const { CACHE_TTL_MS, isSiteAdmin, resetSiteAdminCacheForTests } = await import("./site-admin-config.js");
    resetSiteAdminCacheForTests();

    await expect(isSiteAdmin("alpha")).resolves.toBe(true);
    vi.advanceTimersByTime(CACHE_TTL_MS + 1);
    await expect(isSiteAdmin("alpha")).resolves.toBe(true);
  });
});
