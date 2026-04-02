import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Raid } from "./raidTypes";
import { groupRaidsByTime } from "./raidGrouping";

// Frozen "now" used across all tests
const NOW = "2025-06-15T12:00:00.000Z";

function makeRaid(id: string, startTime: string): Raid {
  return { id, startTime } as Raid;
}

describe("groupRaidsByTime", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(NOW));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("splits mixed raids into upcoming (ASC) and passed (DESC most-recent-first)", () => {
    const raids = [
      makeRaid("past-1", "2025-06-14T10:00:00.000Z"),
      makeRaid("future-1", "2025-06-16T10:00:00.000Z"),
      makeRaid("past-2", "2025-06-15T08:00:00.000Z"),
      makeRaid("future-2", "2025-06-17T10:00:00.000Z"),
    ];

    const { upcoming, passed } = groupRaidsByTime(raids);

    expect(upcoming).toHaveLength(2);
    expect(upcoming[0].id).toBe("future-1");
    expect(upcoming[1].id).toBe("future-2");

    expect(passed).toHaveLength(2);
    // most-recent-first: past-2 (08:00 on the 15th) before past-1 (10:00 on the 14th)
    expect(passed[0].id).toBe("past-2");
    expect(passed[1].id).toBe("past-1");
  });

  it("returns all raids as upcoming when none are in the past", () => {
    const raids = [
      makeRaid("a", "2025-06-16T00:00:00.000Z"),
      makeRaid("b", "2025-06-17T00:00:00.000Z"),
      makeRaid("c", "2025-06-18T00:00:00.000Z"),
    ];

    const { upcoming, passed } = groupRaidsByTime(raids);

    expect(upcoming).toHaveLength(3);
    expect(passed).toHaveLength(0);
  });

  it("returns all raids as passed (DESC) when all are in the past", () => {
    const raids = [
      makeRaid("old-1", "2025-06-10T00:00:00.000Z"),
      makeRaid("old-2", "2025-06-12T00:00:00.000Z"),
      makeRaid("old-3", "2025-06-14T00:00:00.000Z"),
    ];

    const { upcoming, passed } = groupRaidsByTime(raids);

    expect(upcoming).toHaveLength(0);
    expect(passed).toHaveLength(3);
    // most-recent-first
    expect(passed[0].id).toBe("old-3");
    expect(passed[1].id).toBe("old-2");
    expect(passed[2].id).toBe("old-1");
  });

  it("returns empty arrays for empty input", () => {
    const { upcoming, passed } = groupRaidsByTime([]);

    expect(upcoming).toHaveLength(0);
    expect(passed).toHaveLength(0);
  });

  it("treats a raid exactly at 'now' as upcoming (dt < now is false when equal)", () => {
    // startTime equals the frozen NOW exactly
    const raids = [makeRaid("exact", NOW)];

    const { upcoming, passed } = groupRaidsByTime(raids);

    expect(upcoming).toHaveLength(1);
    expect(upcoming[0].id).toBe("exact");
    expect(passed).toHaveLength(0);
  });
});
