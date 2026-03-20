import { describe, expect, it } from "vitest";
import { createReferenceSyncPlan } from "./reference-sync.js";

describe("createReferenceSyncPlan", () => {
  it("plans raw index and detail blob writes under stable reference paths", () => {
    const plan = createReferenceSyncPlan({
      entity: "playable-class",
      indexResponse: {
        _links: { self: { href: "https://example.test/class/index" } },
        classes: [
          { key: { href: "https://example.test/class/1" }, id: 1, name: "Warrior" },
          { key: { href: "https://example.test/class/2" }, id: 2, name: "Paladin" },
        ],
      },
      getDetailIds: (response) => response.classes.map((entry) => entry.id),
      getDetailPath: (id) => `/data/wow/playable-class/${id}`,
    });

    expect(plan.indexBlobName).toBe("reference/playable-class/index.json");
    expect(plan.metaBlobName).toBe("reference/playable-class/meta.json");
    expect(plan.documentCount).toBe(2);
    expect(plan.details).toEqual([
      {
        id: 1,
        blobName: "reference/playable-class/1.json",
        path: "/data/wow/playable-class/1",
      },
      {
        id: 2,
        blobName: "reference/playable-class/2.json",
        path: "/data/wow/playable-class/2",
      },
    ]);
  });
});
