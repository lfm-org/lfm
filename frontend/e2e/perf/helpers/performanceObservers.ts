import type { Page } from "@playwright/test";

export interface StabilityData {
  layoutShifts: { value: number; startTime: number }[];
  longTasks: { duration: number; startTime: number }[];
  cumulativeLayoutShift: number;
}

/**
 * Install browser-side PerformanceObservers for layout-shift and longtask.
 * Call before the measured interaction. Resets accumulated data on each call —
 * do not call more than once per measurement window.
 */
export async function installObservers(page: Page): Promise<void> {
  await page.evaluate(() => {
    const win = window as Record<string, unknown>;
    win.__perfShifts = [];
    win.__perfLongTasks = [];

    new PerformanceObserver((list) => {
      for (const entry of list.getEntries()) {
        if (!(entry as PerformanceEntry & { hadRecentInput?: boolean }).hadRecentInput) {
          (win.__perfShifts as { value: number; startTime: number }[]).push({
            value: (entry as PerformanceEntry & { value: number }).value,
            startTime: entry.startTime,
          });
        }
      }
    }).observe({ type: "layout-shift", buffered: false });

    try {
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          (win.__perfLongTasks as { duration: number; startTime: number }[]).push({
            duration: entry.duration,
            startTime: entry.startTime,
          });
        }
      }).observe({ type: "longtask", buffered: false });
    } catch {
      // longtask may not be supported in all browser contexts
    }
  });
}

/**
 * Collect stability data from browser-side observers.
 * Call after the measured interaction completes.
 */
export async function collectStabilityData(page: Page): Promise<StabilityData> {
  return page.evaluate(() => {
    const win = window as Record<string, unknown>;
    const shifts = (win.__perfShifts ?? []) as { value: number; startTime: number }[];
    const longTasks = (win.__perfLongTasks ?? []) as { duration: number; startTime: number }[];
    const cls = shifts.reduce((sum, s) => sum + s.value, 0);
    return { layoutShifts: shifts, longTasks, cumulativeLayoutShift: cls };
  });
}
