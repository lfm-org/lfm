import { afterEach, describe, expect, it, vi } from "vitest";
import { guildCrestHandler } from "./guild-crest.js";
import type { GuildDocument } from "../types/index.js";

vi.mock("../lib/cosmos.js", () => ({
  getGuildsContainer: vi.fn(),
}));

vi.mock("../lib/blob.js", () => ({
  readBinaryBlob: vi.fn(),
}));

const { getGuildsContainer } = await import("../lib/cosmos.js");
const { readBinaryBlob } = await import("../lib/blob.js");

afterEach(() => {
  vi.clearAllMocks();
});

function makeGuildDoc(overrides: Partial<GuildDocument> = {}): GuildDocument {
  return {
    id: "12345",
    guildId: 12345,
    realmSlug: "test-realm",
    crestBlobName: "guild-crests/12345/crest.svg",
    ...overrides,
  };
}

describe("guildCrestHandler", () => {
  it("returns 404 when the guild document has no mirrored crest", async () => {
    vi.mocked(getGuildsContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: makeGuildDoc({ crestBlobName: undefined }) }),
      })),
    } as never);

    const response = await guildCrestHandler({
      params: { guildId: "12345" },
    } as never, {} as never);

    expect(response.status).toBe(404);
    expect(readBinaryBlob).not.toHaveBeenCalled();
  });

  it("returns the mirrored crest asset with its binary content type", async () => {
    vi.mocked(getGuildsContainer).mockReturnValue({
      item: vi.fn(() => ({
        read: vi.fn().mockResolvedValue({ resource: makeGuildDoc() }),
      })),
    } as never);
    vi.mocked(readBinaryBlob).mockResolvedValue({
      bytes: new Uint8Array([60, 62]),
      contentType: "image/svg+xml",
    });

    const response = await guildCrestHandler({
      params: { guildId: "12345" },
    } as never, {} as never);

    expect(readBinaryBlob).toHaveBeenCalledWith("guild-crests/12345/crest.svg");
    expect(response.status).toBe(200);
    expect(response.headers).toMatchObject({
      "Content-Type": "image/svg+xml",
    });
    expect(response.body).toEqual(Buffer.from([60, 62]));
  });
});
