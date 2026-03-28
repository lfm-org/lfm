import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  TEST_MODE_IDENTITY,
  TEST_MODE_SITE_ADMIN_ACCESS_TOKEN,
  TEST_MODE_SITE_ADMIN_IDENTITY,
} from "./test-mode.js";

const getSecret = vi.fn();
const unsealSession = vi.fn();
const resolveIdentity = vi.fn();
const isSiteAdmin = vi.fn();

vi.mock("@azure/keyvault-secrets", () => ({
  SecretClient: vi.fn(function SecretClient() {
    return { getSecret };
  }),
}));

vi.mock("@azure/identity", () => ({
  DefaultAzureCredential: vi.fn(function DefaultAzureCredential() {}),
}));

vi.mock("./crypto.js", () => ({
  unsealSession,
}));

vi.mock("./site-admin-config.js", () => ({
  isSiteAdmin,
}));

vi.mock("./battlenet.js", () => ({
  battlenet: {
    resolveIdentity,
  },
}));

describe("resolveLocalTestModeAuth", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("accepts the plain deterministic cookie only under the local test-mode guard", async () => {
    const { resolveLocalTestModeAuth } = await import("./auth.js");

    expect(
      resolveLocalTestModeAuth("foo=bar; battlenet_token=test_battlenet_token", {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "http://localhost:8081",
      }),
    ).toEqual({
      identity: TEST_MODE_IDENTITY,
      accessToken: "test_battlenet_token",
    });

    expect(
      resolveLocalTestModeAuth("battlenet_token=test_battlenet_token", {
        TEST_MODE: "true",
        COSMOS_ENDPOINT: "https://localhost:8081",
      }),
    ).toBeNull();
  });
});

describe("requireSiteAdminAuthWithToken", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns the auth payload when the async resolver allows the user", async () => {
    unsealSession.mockResolvedValue("access-token");
    resolveIdentity.mockResolvedValue({
      battleNetId: "admin-hash",
      guildId: null,
      guildName: null,
    });
    isSiteAdmin.mockResolvedValue(true);

    const { requireSiteAdminAuthWithToken } = await import("./auth.js");
    const request = {
      headers: {
        get: vi.fn(() => "battlenet_token=sealed-token"),
      },
    } as never;

    await expect(requireSiteAdminAuthWithToken(request)).resolves.toEqual({
      accessToken: "access-token",
      identity: {
        battleNetId: "admin-hash",
        guildId: null,
        guildName: null,
      },
      isSiteAdmin: true,
    });
    expect(isSiteAdmin).toHaveBeenCalledWith("admin-hash");
  });

  it("returns null when the async resolver denies the user", async () => {
    unsealSession.mockResolvedValue("access-token");
    resolveIdentity.mockResolvedValue({
      battleNetId: "member-hash",
      guildId: null,
      guildName: null,
    });
    isSiteAdmin.mockResolvedValue(false);

    const { requireSiteAdminAuthWithToken } = await import("./auth.js");
    const request = {
      headers: {
        get: vi.fn(() => "battlenet_token=sealed-token"),
      },
    } as never;

    await expect(requireSiteAdminAuthWithToken(request)).resolves.toBeNull();
  });

  it("accepts the local test-mode site-admin cookie without consulting Key Vault", async () => {
    vi.doUnmock("./site-admin-config.js");
    const originalTestMode = process.env.TEST_MODE;
    const originalCosmosEndpoint = process.env.COSMOS_ENDPOINT;
    process.env.TEST_MODE = "true";
    process.env.COSMOS_ENDPOINT = "http://localhost:8081";

    const { requireSiteAdminAuthWithToken } = await import("./auth.js");

    try {
      const request = {
        headers: {
          get: vi.fn(
            () => `battlenet_token=${encodeURIComponent(TEST_MODE_SITE_ADMIN_ACCESS_TOKEN)}`,
          ),
        },
      } as never;

      await expect(requireSiteAdminAuthWithToken(request)).resolves.toEqual({
        accessToken: TEST_MODE_SITE_ADMIN_ACCESS_TOKEN,
        identity: TEST_MODE_SITE_ADMIN_IDENTITY,
        isSiteAdmin: true,
      });
      expect(getSecret).not.toHaveBeenCalled();
    } finally {
      process.env.TEST_MODE = originalTestMode;
      process.env.COSMOS_ENDPOINT = originalCosmosEndpoint;
    }
  });
});
