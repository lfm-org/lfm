import { afterEach, describe, expect, it, vi } from "vitest";
import { adminGuildSettingsHandler, currentGuildHandler, currentGuildSettingsHandler } from "./guild.js";
import { requireAuthWithToken, requireSiteAdminAuthWithToken } from "../lib/auth.js";
import { BlizzardGuildRefreshError, loadCurrentGuildHome, saveAdminGuildSettings, saveCurrentGuildSettings } from "../lib/guild/service.js";

vi.mock("../lib/auth.js", () => ({
  requireAuthWithToken: vi.fn(),
  requireSiteAdminAuthWithToken: vi.fn(),
}));

vi.mock("../lib/audit.js", () => ({
  auditLog: vi.fn(),
}));

vi.mock("../lib/guild/service.js", () => {
  class MockBlizzardGuildRefreshError extends Error {
    constructor(message = "Failed to fetch guild profile from Blizzard") {
      super(message);
      this.name = "BlizzardGuildRefreshError";
    }
  }

  return {
    BlizzardGuildRefreshError: MockBlizzardGuildRefreshError,
    loadCurrentGuildHome: vi.fn(),
    saveCurrentGuildSettings: vi.fn(),
    saveAdminGuildSettings: vi.fn(),
    loadAdminGuildHome: vi.fn(),
    resolveAdminGuild: vi.fn(),
  };
});

afterEach(() => {
  vi.clearAllMocks();
});

function makeContext() {
  return {
    log: vi.fn(),
  } as never;
}

describe("currentGuildHandler", () => {
  it("returns the Blizzard refresh 502 only for the refresh sentinel", async () => {
    vi.mocked(requireAuthWithToken).mockResolvedValue({
      identity: { guildId: 12345, guildName: "Test Guild", battleNetId: "bnet-1" },
      accessToken: "token",
    });
    vi.mocked(loadCurrentGuildHome).mockRejectedValue(new BlizzardGuildRefreshError());

    const response = await currentGuildHandler({} as never, makeContext());

    expect(response.status).toBe(502);
    expect(JSON.parse(response.body as string)).toEqual({
      error: "Failed to fetch guild profile from Blizzard",
    });
  });

  it("lets non-refresh failures bubble out", async () => {
    vi.mocked(requireAuthWithToken).mockResolvedValue({
      identity: { guildId: 12345, guildName: "Test Guild", battleNetId: "bnet-1" },
      accessToken: "token",
    });
    vi.mocked(loadCurrentGuildHome).mockRejectedValue(new Error("storage failure"));

    await expect(currentGuildHandler({} as never, makeContext())).rejects.toThrow("storage failure");
  });
});

describe("currentGuildSettingsHandler", () => {
  it("maps malformed JSON to the prior 400 response", async () => {
    vi.mocked(requireAuthWithToken).mockResolvedValue({
      identity: { guildId: 12345, guildName: "Test Guild", battleNetId: "bnet-1" },
      accessToken: "token",
    });

    const response = await currentGuildSettingsHandler({
      json: vi.fn().mockRejectedValue(new Error("bad json")),
    } as never, makeContext());

    expect(response.status).toBe(400);
    expect(JSON.parse(response.body as string)).toEqual({
      error: "Invalid guild settings payload",
    });
    expect(saveCurrentGuildSettings).not.toHaveBeenCalled();
  });
});

describe("adminGuildSettingsHandler", () => {
  it("maps malformed JSON to the prior 400 response", async () => {
    vi.mocked(requireSiteAdminAuthWithToken).mockResolvedValue({
      identity: { guildId: null, guildName: null, battleNetId: "admin-bnet" },
      accessToken: "token",
      isSiteAdmin: true,
    });

    const response = await adminGuildSettingsHandler({
      params: { guildId: "12345" },
      json: vi.fn().mockRejectedValue(new Error("bad json")),
    } as never, makeContext());

    expect(response.status).toBe(400);
    expect(JSON.parse(response.body as string)).toEqual({
      error: "Invalid guild settings payload",
    });
    expect(saveAdminGuildSettings).not.toHaveBeenCalled();
  });
});
