import { beforeEach, describe, expect, it, vi } from "vitest";

const mockRequireAuth = vi.fn();
const mockGetRaidersContainer = vi.fn();

vi.mock("../lib/auth.js", () => ({
  requireAuth: mockRequireAuth,
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: mockGetRaidersContainer,
}));

function makeRequest(body: unknown) {
  return { json: () => Promise.resolve(body) } as never;
}

describe("meUpdateHandler", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("returns 401 when auth is missing", async () => {
    mockRequireAuth.mockResolvedValue(null);

    const { meUpdateHandler } = await import("./me-update.js");
    const response = await meUpdateHandler(makeRequest({}), {} as never);

    expect(response.status).toBe(401);
    expect(JSON.parse(response.body as string)).toEqual({ error: "Unauthorized" });
  });

  it("updates locale on the raider document", async () => {
    mockRequireAuth.mockResolvedValue({ battleNetId: "bnet-1", guildName: "G" });
    const mockReplace = vi.fn().mockResolvedValue({});
    mockGetRaidersContainer.mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({
          resource: {
            id: "bnet-1",
            battleNetId: "bnet-1",
            selectedCharacterId: null,
            createdAt: "2026-01-01T00:00:00Z",
            lastSeenAt: "2026-01-01T00:00:00Z",
            characters: [],
          },
        }),
        replace: mockReplace,
      })),
    });

    const { meUpdateHandler } = await import("./me-update.js");
    const response = await meUpdateHandler(makeRequest({ locale: "fi" }), {} as never);

    expect(response.status).toBe(200);
    expect(JSON.parse(response.body as string)).toEqual({ locale: "fi" });
    expect(mockReplace).toHaveBeenCalledWith(
      expect.objectContaining({ locale: "fi", battleNetId: "bnet-1" })
    );
  });

  it("rejects invalid locale values", async () => {
    mockRequireAuth.mockResolvedValue({ battleNetId: "bnet-1", guildName: "G" });

    const { meUpdateHandler } = await import("./me-update.js");
    const response = await meUpdateHandler(makeRequest({ locale: "de" }), {} as never);

    expect(response.status).toBe(400);
    expect(JSON.parse(response.body as string)).toEqual({
      error: "Invalid locale. Supported: en, fi",
    });
  });

  it("returns 404 when raider document is missing", async () => {
    mockRequireAuth.mockResolvedValue({ battleNetId: "bnet-1", guildName: "G" });
    mockGetRaidersContainer.mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: undefined }),
      })),
    });

    const { meUpdateHandler } = await import("./me-update.js");
    const response = await meUpdateHandler(makeRequest({ locale: "fi" }), {} as never);

    expect(response.status).toBe(404);
    expect(JSON.parse(response.body as string)).toEqual({ error: "Raider not found" });
  });
});
