import { describe, expect, it, vi } from "vitest";

import { syncReferenceEntities } from "./reference-sync-live.js";

describe("syncReferenceEntities", () => {
  const definition = {
    name: "classes",
    entity: "playable-class",
    maxAgeMs: 2 * 60 * 60 * 1000,
    fetchIndex: vi.fn(async () => ({
      classes: [
        {
          id: 1,
          key: { href: "/data/wow/playable-class/1" },
        },
      ],
    })),
    getDetails: vi.fn((response: { classes: Array<{ id: number; key: { href: string } }> }) =>
      response.classes.map((entry) => ({
        id: entry.id,
        href: entry.key.href,
      }))
    ),
  };

  function createIo(meta: unknown = null) {
    const blobs = new Map<string, unknown>();
    if (meta) blobs.set("reference/playable-class/meta.json", meta);

    return {
      blobs,
      readBlob: vi.fn(async (blobName: string) => blobs.get(blobName) ?? null),
      writeBlob: vi.fn(async (blobName: string, data: unknown) => {
        blobs.set(blobName, data);
      }),
      fetchToken: vi.fn(async () => "live-token"),
      fetchJson: vi.fn(async (href: string) => {
        if (href === "/data/wow/playable-class/1") {
          return { id: 1, name: "Warrior" };
        }

        throw new Error(`Unexpected href: ${href}`);
      }),
      sleep: vi.fn(async () => undefined),
      now: vi.fn(() => new Date("2026-03-20T12:00:00.000Z")),
      log: vi.fn(),
    };
  }

  it("skips syncing when the cached data is still fresh", async () => {
    const io = createIo({
      lastSuccessTime: "2026-03-20T11:30:00.000Z",
      lastFailureTime: null,
      lastFailureReason: null,
    });

    const result = await syncReferenceEntities([definition], io);

    expect(result).toEqual({
      results: [{ name: "classes", status: "skipped (fresh)" }],
    });
    expect(io.fetchToken).not.toHaveBeenCalled();
    expect(definition.fetchIndex).not.toHaveBeenCalled();
    expect(io.fetchJson).not.toHaveBeenCalled();
  });

  it("syncs the reference set when the cache is stale", async () => {
    const io = createIo({
      lastSuccessTime: "2026-03-20T08:00:00.000Z",
      lastFailureTime: null,
      lastFailureReason: null,
    });

    const result = await syncReferenceEntities([definition], io);

    expect(result).toEqual({
      results: [{ name: "classes", status: "synced (1 docs)" }],
    });
    expect(io.fetchToken).toHaveBeenCalledTimes(1);
    expect(definition.fetchIndex).toHaveBeenCalledWith("live-token");
    expect(io.fetchJson).toHaveBeenCalledWith("/data/wow/playable-class/1", "live-token");
    expect(io.writeBlob).toHaveBeenCalledWith(
      "reference/playable-class/index.json",
      expect.objectContaining({
        classes: expect.any(Array),
      })
    );
    expect(io.writeBlob).toHaveBeenCalledWith("reference/playable-class/1.json", {
      id: 1,
      name: "Warrior",
    });
    expect(io.writeBlob).toHaveBeenCalledWith(
      "reference/playable-class/meta.json",
      expect.objectContaining({
        lastSuccessTime: "2026-03-20T12:00:00.000Z",
        lastFailureTime: null,
        lastFailureReason: null,
      })
    );
  });

  it("force refresh ignores fresh metadata", async () => {
    const io = createIo({
      lastSuccessTime: "2026-03-20T11:45:00.000Z",
      lastFailureTime: null,
      lastFailureReason: null,
    });

    const result = await syncReferenceEntities([definition], io, { force: true });

    expect(result).toEqual({
      results: [{ name: "classes", status: "synced (1 docs)" }],
    });
    expect(io.fetchToken).toHaveBeenCalledTimes(1);
    expect(definition.fetchIndex).toHaveBeenCalledWith("live-token");
  });
});
