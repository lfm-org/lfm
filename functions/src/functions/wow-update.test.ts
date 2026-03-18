import { describe, expect, it } from "vitest";
import { toWowInstance } from "./wow-update.js";

describe("toWowInstance", () => {
  it("enriches Blizzard instance modes with tracked metadata and modeKey", () => {
    const instance = toWowInstance({
      id: 631,
      name: "Icecrown Citadel",
      category: { type: "RAID" },
      expansion: { id: 3 },
      minimum_level: 80,
      modes: [
        {
          mode: {
            type: "NORMAL",
            name: "Normal",
          },
          players: 10,
          is_tracked: true,
        },
        {
          mode: {
            type: "HEROIC",
            name: "Heroic",
          },
          players: 25,
          is_tracked: false,
        },
      ],
    });

    expect(instance).toEqual({
      id: 631,
      name: "Icecrown Citadel",
      type: "RAID",
      minLevel: 80,
      expansionId: 3,
      modes: [
        {
          type: "NORMAL",
          name: "Normal",
          players: 10,
          isTracked: true,
          modeKey: "NORMAL:10",
        },
        {
          type: "HEROIC",
          name: "Heroic",
          players: 25,
          isTracked: false,
          modeKey: "HEROIC:25",
        },
      ],
    });
  });
});
