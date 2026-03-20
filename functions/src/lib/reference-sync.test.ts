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
      getDetails: (response) =>
        response.classes.map((entry) => ({
          id: entry.id,
          href: entry.key.href,
        })),
    });

    expect(plan.indexBlobName).toBe("reference/playable-class/index.json");
    expect(plan.metaBlobName).toBe("reference/playable-class/meta.json");
    expect(plan.documentCount).toBe(2);
    expect(plan.details).toEqual([
      {
        id: 1,
        blobName: "reference/playable-class/1.json",
        href: "https://example.test/class/1",
      },
      {
        id: 2,
        blobName: "reference/playable-class/2.json",
        href: "https://example.test/class/2",
      },
    ]);
  });

  it("preserves Blizzard-provided versioned specialization hrefs", () => {
    const plan = createReferenceSyncPlan({
      entity: "playable-specialization",
      indexResponse: {
        _links: { self: { href: "https://eu.api.blizzard.com/data/wow/playable-specialization/index?namespace=static-eu" } },
        character_specializations: [
          {
            key: {
              href: "https://eu.api.blizzard.com/data/wow/playable-specialization/62?namespace=static-11.2.0_62213-eu",
            },
            id: 62,
            name: "Arcane",
          },
        ],
      },
      getDetails: (response) =>
        response.character_specializations.map((entry) => ({
          id: entry.id,
          href: entry.key.href,
        })),
    });

    expect(plan.details).toEqual([
      {
        id: 62,
        blobName: "reference/playable-specialization/62.json",
        href: "https://eu.api.blizzard.com/data/wow/playable-specialization/62?namespace=static-11.2.0_62213-eu",
      },
    ]);
  });
});
