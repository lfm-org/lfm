import { describe, expect, it } from "vitest";
import { formatInstanceModeLabel, resolveInstanceModeLabel, toModeKey, type WowInstance } from "./instances";

describe("wow instance helpers", () => {
  it("builds mode keys and labels from nested mode objects", () => {
    const instances: WowInstance[] = [
      {
        id: 63,
        name: "Deadmines",
        type: "DUNGEON",
        minLevel: 35,
        expansionId: 68,
        modes: [{ mode: { type: "NORMAL", name: "Normal" }, players: 5, is_tracked: true }],
      },
    ];

    expect(toModeKey(instances[0].modes[0])).toBe("NORMAL:5");
    expect(formatInstanceModeLabel(instances[0].modes[0])).toBe("Normal (5 players)");
    expect(resolveInstanceModeLabel(instances, 63, "NORMAL:5")).toBe("Normal (5 players)");
  });
});
