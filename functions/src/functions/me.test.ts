import { beforeEach, describe, expect, it, vi } from "vitest";

const mockRequireAuth = vi.fn();
const mockIsSiteAdmin = vi.fn();
const mockGetRaidersContainer = vi.fn();

vi.mock("../lib/auth.js", () => ({
  requireAuth: mockRequireAuth,
}));

vi.mock("../lib/site-admin-config.js", () => ({
  isSiteAdmin: mockIsSiteAdmin,
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: mockGetRaidersContainer,
}));

describe("meHandler", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns the current payload when the async site-admin resolver allows the user", async () => {
    mockRequireAuth.mockResolvedValue({
      battleNetId: "bnet-1",
      guildName: "Test Guild",
    });
    mockIsSiteAdmin.mockResolvedValue(true);
    mockGetRaidersContainer.mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({
          resource: { selectedCharacterId: "char-1" },
        }),
      })),
    });

    const { meHandler } = await import("./me.js");
    const response = await meHandler({} as never, {} as never);

    expect(response.status).toBe(200);
    expect(JSON.parse(response.body as string)).toEqual({
      battleNetId: "bnet-1",
      guildName: "Test Guild",
      selectedCharacterId: "char-1",
      isSiteAdmin: true,
    });
    expect(mockIsSiteAdmin).toHaveBeenCalledWith("bnet-1");
  });

  it("returns the current payload when the async site-admin resolver denies the user", async () => {
    mockRequireAuth.mockResolvedValue({
      battleNetId: "bnet-2",
      guildName: "Another Guild",
    });
    mockIsSiteAdmin.mockResolvedValue(false);
    mockGetRaidersContainer.mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({
          resource: { selectedCharacterId: null },
        }),
      })),
    });

    const { meHandler } = await import("./me.js");
    const response = await meHandler({} as never, {} as never);

    expect(response.status).toBe(200);
    expect(JSON.parse(response.body as string)).toEqual({
      battleNetId: "bnet-2",
      guildName: "Another Guild",
      selectedCharacterId: null,
      isSiteAdmin: false,
    });
    expect(mockIsSiteAdmin).toHaveBeenCalledWith("bnet-2");
  });

  it("returns 401 when auth is missing", async () => {
    mockRequireAuth.mockResolvedValue(null);

    const { meHandler } = await import("./me.js");
    const response = await meHandler({} as never, {} as never);

    expect(response.status).toBe(401);
    expect(JSON.parse(response.body as string)).toEqual({
      error: "Unauthorized",
    });
    expect(mockIsSiteAdmin).not.toHaveBeenCalled();
    expect(mockGetRaidersContainer).not.toHaveBeenCalled();
  });
});
