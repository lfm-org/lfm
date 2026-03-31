import { DateTime } from "luxon";
import type { Raid } from "./raidTypes";

export interface GroupedRaids {
  upcoming: Raid[];
  passed: Raid[];
}

export function groupRaidsByTime(raids: Raid[]): GroupedRaids {
  const now = DateTime.now();
  const upcoming: Raid[] = [];
  const passed: Raid[] = [];

  for (const raid of raids) {
    const dt = DateTime.fromISO(raid.startTime, { zone: "UTC" });
    if (dt.isValid && dt < now) {
      passed.push(raid);
    } else {
      upcoming.push(raid);
    }
  }

  // upcoming is already ASC from API; reverse passed so most-recent-first
  passed.reverse();

  return { upcoming, passed };
}
