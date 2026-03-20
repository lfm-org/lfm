import { describe, expect, it } from "vitest";
import { normalizeWowInstances } from "./wowInstances.js";

describe("normalizeWowInstances", () => {
  it("keeps the current flattened mode shape intact", () => {
    expect(
      normalizeWowInstances([
        {
          id: 631,
          name: "Icecrown Citadel",
          type: "RAID",
          minLevel: 80,
          expansionId: 3,
          modes: [
            {
              type: "HEROIC",
              name: "Heroic",
              players: 25,
              isTracked: true,
              modeKey: "HEROIC:25",
            },
          ],
        },
      ])
    ).toEqual([
      {
        id: 631,
        name: "Icecrown Citadel",
        type: "RAID",
        minLevel: 80,
        expansionId: 3,
        modes: [
          {
            type: "HEROIC",
            name: "Heroic",
            players: 25,
            isTracked: true,
            modeKey: "HEROIC:25",
          },
        ],
      },
    ]);
  });

  it("normalizes legacy nested Blizzard mode objects into the flattened app shape", () => {
    expect(
      normalizeWowInstances([
        {
          id: 63,
          name: "Deadmines",
          type: "DUNGEON",
          minLevel: 35,
          expansionId: 68,
          modes: [
            {
              mode: {
                type: "NORMAL",
                name: "Normal",
              },
              players: 5,
              is_tracked: true,
            },
            {
              mode: {
                type: "HEROIC",
                name: "Heroic",
              },
              players: 5,
            },
          ],
        },
      ])
    ).toEqual([
      {
        id: 63,
        name: "Deadmines",
        type: "DUNGEON",
        minLevel: 35,
        expansionId: 68,
        modes: [
          {
            type: "NORMAL",
            name: "Normal",
            players: 5,
            isTracked: true,
            modeKey: "NORMAL:5",
          },
          {
            type: "HEROIC",
            name: "Heroic",
            players: 5,
            isTracked: false,
            modeKey: "HEROIC:5",
          },
        ],
      },
    ]);
  });
});
