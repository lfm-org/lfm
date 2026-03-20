import { describe, expect, it } from "vitest";
import { findModeByKey, hasModeKey, normalizeWowInstance, toModeKey } from "./wow-instance-modes.js";
import type { WowInstance } from "../types/index.js";

describe("wow-instance-modes", () => {
  it("supports legacy flattened modes while cached data is refreshed", () => {
    const legacyInstance = {
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
      ],
    } as unknown as WowInstance;

    expect(toModeKey(legacyInstance.modes[0])).toBe("NORMAL:5");
    expect(hasModeKey(legacyInstance, "NORMAL:5")).toBe(true);
    expect(findModeByKey(legacyInstance, "NORMAL:5")).toMatchObject({
      players: 5,
    });
    expect(normalizeWowInstance(legacyInstance)).toEqual({
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
      ],
    });
  });
});
