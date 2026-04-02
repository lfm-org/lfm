import { DateTime } from "luxon";
import type { Run } from "./runTypes";

export interface GroupedRuns {
  upcoming: Run[];
  passed: Run[];
}

export function groupRunsByTime(runs: Run[]): GroupedRuns {
  const now = DateTime.now();
  const upcoming: Run[] = [];
  const passed: Run[] = [];

  for (const run of runs) {
    const dt = DateTime.fromISO(run.startTime, { zone: "UTC" });
    if (dt.isValid && dt < now) {
      passed.push(run);
    } else {
      upcoming.push(run);
    }
  }

  // upcoming is already ASC from API; reverse passed so most-recent-first
  passed.reverse();

  return { upcoming, passed };
}
