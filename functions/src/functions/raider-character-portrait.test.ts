import { afterEach, describe, expect, it, vi } from "vitest";
import { characterPortraitHandler } from "./raider-character-portrait.js";
import type { RaiderDocument } from "../types/index.js";

vi.mock("../lib/auth.js", () => ({
  requireAuth: vi.fn(),
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: vi.fn(),
}));

vi.mock("../lib/blob.js", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/blob.js")>();
  return {
    ...actual,
    readBinaryBlob: vi.fn(),
  };
});

const { requireAuth } = await import("../lib/auth.js");
const { getRaidersContainer } = await import("../lib/cosmos.js");
const { readBinaryBlob } = await import("../lib/blob.js");

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
      "eu-test-realm-aelrin": "/api/raider/character-portrait/eu-test-realm-aelrin/jpg",
    },
    ...overrides,
  };
}

describe("characterPortraitHandler", () => {
  it("streams a mirrored portrait for an authenticated cached character", async () => {
    vi.mocked(requireAuth).mockResolvedValue({
      battleNetId: "bnet-1",
      guildId: null,
      guildName: null,
    });
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: makeRaiderDoc() }),
      })),
    } as never);
    vi.mocked(readBinaryBlob).mockResolvedValue({
      bytes: new Uint8Array([0xff, 0xd8, 0xff]),
      contentType: "image/jpeg",
    });

    const response = await characterPortraitHandler({
      params: {
        characterId: "eu-test-realm-aelrin",
        format: "jpg",
      },
    } as never, {} as never);

    expect(readBinaryBlob).toHaveBeenCalledWith("character-portraits/eu-test-realm-aelrin.jpg");
    expect(response.status).toBe(200);
    expect(response.headers).toMatchObject({
      "Content-Type": "image/jpeg",
    });
    expect(response.body).toEqual(Buffer.from([0xff, 0xd8, 0xff]));
  });
});
