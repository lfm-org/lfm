import { afterEach, describe, expect, it, vi } from "vitest";
import { handler } from "./battlenet-character-portraits.js";
import type { RaiderDocument } from "../types/index.js";

vi.mock("../lib/auth.js", () => ({
  requireAuthWithToken: vi.fn(),
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: vi.fn(),
}));

const { requireAuthWithToken } = await import("../lib/auth.js");
const { getRaidersContainer } = await import("../lib/cosmos.js");

afterEach(() => {
  vi.clearAllMocks();
});

function makeRaiderDoc(overrides: Partial<RaiderDocument> = {}): RaiderDocument {
  return {
    id: "bnet-1",
    battleNetId: "bnet-1",
    selectedCharacterId: null,
    createdAt: "2026-03-28T00:00:00.000Z",
    lastSeenAt: "2026-03-28T00:00:00.000Z",
    characters: [],
    portraitCache: {
      "eu-test-realm-aelrin": "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
    },
    ...overrides,
  };
}

describe("handler", () => {
  it("filters out stale blob URLs from portraitCache", async () => {
    vi.mocked(requireAuthWithToken).mockResolvedValue({
      identity: { battleNetId: "bnet-1", guildId: null, guildName: null },
      accessToken: "token",
    });

    vi.mocked(getRaidersContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({
          resource: makeRaiderDoc({
            portraitCache: {
              "eu-test-realm-aelrin": "https://lfmstore.blob.core.windows.net/wow/character-portraits/eu-test-realm-aelrin.jpg",
            },
          }),
        }),
      })),
    } as never);

    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
    });

    const response = await handler({
      json: vi.fn().mockResolvedValue([
        { region: "eu", realm: "test-realm", name: "Aelrin" },
      ]),
    } as never, { log: vi.fn() } as never);

    expect(JSON.parse(response.body as string)).toEqual({});
  });

  it("returns a cached Blizzard CDN portrait URL directly without mirroring", async () => {
    vi.mocked(requireAuthWithToken).mockResolvedValue({
      identity: { battleNetId: "bnet-1", guildId: null, guildName: null },
      accessToken: "token",
    });

    const replace = vi.fn().mockResolvedValue(undefined);
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: makeRaiderDoc() }),
        replace,
      })),
    } as never);

    const response = await handler({
      json: vi.fn().mockResolvedValue([
        { region: "eu", realm: "test-realm", name: "Aelrin" },
      ]),
    } as never, { log: vi.fn() } as never);

    expect(replace).not.toHaveBeenCalled();
    expect(JSON.parse(response.body as string)).toEqual({
      "eu-test-realm-aelrin": "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
    });
  });
});
