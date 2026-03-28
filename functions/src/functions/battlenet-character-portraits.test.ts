import { afterEach, describe, expect, it, vi } from "vitest";
import { handler } from "./battlenet-character-portraits.js";
import type { RaiderDocument } from "../types/index.js";

vi.mock("../lib/auth.js", () => ({
  requireAuthWithToken: vi.fn(),
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: vi.fn(),
}));

vi.mock("../lib/character-portrait.js", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/character-portrait.js")>();
  return {
    ...actual,
    syncCharacterPortrait: vi.fn(),
  };
});

const { requireAuthWithToken } = await import("../lib/auth.js");
const { getRaidersContainer } = await import("../lib/cosmos.js");
const { syncCharacterPortrait } = await import("../lib/character-portrait.js");

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
  it("repairs cached Blizzard portrait URLs into mirrored local URLs before returning them", async () => {
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
    vi.mocked(syncCharacterPortrait).mockResolvedValue({
      portraitBlobName: "character-portraits/eu-test-realm-aelrin.jpg",
      portraitUrl: "/api/raider/character-portrait/eu-test-realm-aelrin/jpg",
    });

    const response = await handler({
      json: vi.fn().mockResolvedValue([
        { region: "eu", realm: "test-realm", name: "Aelrin" },
      ]),
    } as never, { log: vi.fn() } as never);

    expect(syncCharacterPortrait).toHaveBeenCalledWith(
      "eu-test-realm-aelrin",
      "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
      expect.any(Object)
    );
    expect(replace).toHaveBeenCalledTimes(1);
    expect(JSON.parse(response.body as string)).toEqual({
      "eu-test-realm-aelrin": "/api/raider/character-portrait/eu-test-realm-aelrin/jpg",
    });
  });
});
