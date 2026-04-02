import { afterEach, describe, expect, it, vi } from "vitest";
import { listHandler } from "./raider-character.js";
import type { RaiderDocument, StoredSelectedCharacter } from "../types/index.js";

vi.mock("../lib/auth.js", () => ({
  requireAuth: vi.fn(),
  requireAuthWithToken: vi.fn(),
}));

vi.mock("../lib/cosmos.js", () => ({
  getRaidersContainer: vi.fn(),
}));

vi.mock("../lib/reference-data.js", () => ({
  readWowSpecializationMap: vi.fn(),
}));

const { requireAuth } = await import("../lib/auth.js");
const { getRaidersContainer } = await import("../lib/cosmos.js");
const { readWowSpecializationMap } = await import("../lib/reference-data.js");

afterEach(() => {
  vi.clearAllMocks();
});

function makeStoredCharacter(overrides: Partial<StoredSelectedCharacter> = {}): StoredSelectedCharacter {
  return {
    id: "eu-test-realm-aelrin",
    region: "eu",
    realm: "test-realm",
    name: "Aelrin",
    fetchedAt: "2026-03-28T00:00:00.000Z",
    profileSummary: {
      name: "Aelrin",
      level: 80,
      realm: { slug: "test-realm", name: { en_US: "Test Realm" } },
      character_class: { id: 2, name: "Paladin" },
      race: { id: 11, name: "Draenei" },
    },
    mediaSummary: {
      assets: [
        {
          key: "avatar",
          value: "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
        },
      ],
    },
    specializationsSummary: {
      specializations: [{ specialization: { id: 65, name: "Holy" } }],
      active_specialization: { id: 65, name: "Holy" },
    },
    ...overrides,
  };
}

function makeRaiderDoc(overrides: Partial<RaiderDocument> = {}): RaiderDocument {
  return {
    id: "bnet-1",
    battleNetId: "bnet-1",
    selectedCharacterId: "eu-test-realm-aelrin",
    createdAt: "2026-03-28T00:00:00.000Z",
    lastSeenAt: "2026-03-28T00:00:00.000Z",
    characters: [makeStoredCharacter()],
    ...overrides,
  };
}

describe("listHandler", () => {
  it("extracts CDN portrait URL from mediaSummary and stores it without blob mirroring", async () => {
    vi.mocked(requireAuth).mockResolvedValue({
      battleNetId: "bnet-1",
      guildId: null,
      guildName: null,
    });

    const replace = vi.fn().mockResolvedValue(undefined);
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: makeRaiderDoc() }),
        replace,
      })),
    } as never);
    vi.mocked(readWowSpecializationMap).mockResolvedValue(new Map([
      [65, { id: 65, name: "Holy", classId: 2, role: "HEALER" }],
    ]));

    const response = await listHandler({} as never, { log: vi.fn() } as never);

    // Portrait URL comes directly from mediaSummary avatar — no blob mirroring
    expect(replace).toHaveBeenCalledTimes(1);
    expect(JSON.parse(response.body as string)).toEqual({
      characters: [
        {
          id: "eu-test-realm-aelrin",
          region: "eu",
          realm: "test-realm",
          name: "Aelrin",
          level: 80,
          classId: 2,
          raceId: 11,
          portraitUrl: "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg",
          fetchedAt: "2026-03-28T00:00:00.000Z",
          specializations: [{ id: 65, name: "Holy", role: "HEALER" }],
          activeSpecId: 65,
        },
      ],
      selectedCharacterId: "eu-test-realm-aelrin",
    });
  });

  it("does not update the database when portraits are already up to date", async () => {
    const cdnUrl = "https://render.worldofwarcraft.com/eu/character/stormreaver/69/172412997-avatar.jpg";
    vi.mocked(requireAuth).mockResolvedValue({
      battleNetId: "bnet-1",
      guildId: null,
      guildName: null,
    });

    const replace = vi.fn().mockResolvedValue(undefined);
    vi.mocked(getRaidersContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({
          resource: makeRaiderDoc({
            characters: [makeStoredCharacter({ portraitUrl: cdnUrl })],
            portraitCache: { "eu-test-realm-aelrin": cdnUrl },
          }),
        }),
        replace,
      })),
    } as never);
    vi.mocked(readWowSpecializationMap).mockResolvedValue(new Map([
      [65, { id: 65, name: "Holy", classId: 2, role: "HEALER" }],
    ]));

    await listHandler({} as never, { log: vi.fn() } as never);

    expect(replace).not.toHaveBeenCalled();
  });
});
