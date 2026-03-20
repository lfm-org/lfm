import assert from "node:assert/strict";
import test from "node:test";
import {
  formatInstanceModeLabel,
  resolveInstanceModeLabel,
  toModeKey,
  type WowInstance,
} from "../src/lib/wowInstances.ts";

test("wow instance helpers use nested mode objects", () => {
  const instances = [
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
      ],
    } satisfies WowInstance,
  ];

  assert.equal(toModeKey(instances[0].modes[0]), "NORMAL:5");
  assert.equal(formatInstanceModeLabel(instances[0].modes[0]), "Normal (5 players)");
  assert.equal(resolveInstanceModeLabel(instances, 63, "NORMAL:5"), "Normal (5 players)");
});
