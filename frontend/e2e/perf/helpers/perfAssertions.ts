import { expect, type Locator, type Page } from "@playwright/test";
import { installObservers, collectStabilityData, type StabilityData } from "./performanceObservers";
import { STABILITY } from "./flowBudgets";

export interface InteractionResult {
  ackMs: number;
  completionMs: number;
  stability: StabilityData;
}

export interface MeasureOptions {
  /** Locator that signals immediate visual acknowledgement */
  ackMarker: Locator;
  /** Locator that signals the flow is complete and stable */
  completionMarker: Locator;
  /** Maximum time to wait for markers before timing out (ms). Defaults to 5000. */
  timeout?: number;
}

/**
 * Measure a single user interaction's responsiveness.
 *
 * Timing uses Node-side performance.now(), which includes Playwright protocol
 * overhead (WebSocket round-trips for waitFor). This is a pragmatic trade-off:
 * the overhead is small relative to the budgets, and the measurement captures
 * the same signal a user would perceive.
 *
 * 1. Installs browser observers for stability tracking
 * 2. Records start time
 * 3. Executes the action
 * 4. Waits for acknowledgement marker to become visible
 * 5. Waits for completion marker to become visible
 * 6. Collects stability data from the browser
 */
export async function measureInteraction(
  page: Page,
  action: () => Promise<void>,
  options: MeasureOptions,
): Promise<InteractionResult> {
  const timeout = options.timeout ?? 5_000;

  await installObservers(page);

  const start = performance.now();
  await action();

  await options.ackMarker.waitFor({ state: "visible", timeout });
  const ackMs = performance.now() - start;

  await options.completionMarker.waitFor({ state: "visible", timeout });
  const completionMs = performance.now() - start;

  const stability = await collectStabilityData(page);

  return { ackMs, completionMs, stability };
}

/** Assert acknowledgement happened within budget. */
export function expectAcknowledgementWithin(
  result: InteractionResult,
  budgetMs: number,
): void {
  expect(
    result.ackMs,
    `Acknowledgement took ${result.ackMs.toFixed(0)}ms, budget is ${budgetMs}ms`,
  ).toBeLessThanOrEqual(budgetMs);
}

/** Assert completion happened within budget. */
export function expectCompletionWithin(
  result: InteractionResult,
  budgetMs: number,
): void {
  expect(
    result.completionMs,
    `Completion took ${result.completionMs.toFixed(0)}ms, budget is ${budgetMs}ms`,
  ).toBeLessThanOrEqual(budgetMs);
}

/** Assert no major layout instability during the interaction window. */
export function expectStableInteraction(result: InteractionResult): void {
  expect(
    result.stability.cumulativeLayoutShift,
    `CLS ${result.stability.cumulativeLayoutShift.toFixed(4)} exceeds max ${STABILITY.MAX_CLS}`,
  ).toBeLessThanOrEqual(STABILITY.MAX_CLS);

  for (const shift of result.stability.layoutShifts) {
    expect(
      shift.value,
      `Single layout shift ${shift.value.toFixed(4)} exceeds max ${STABILITY.MAX_SINGLE_SHIFT}`,
    ).toBeLessThanOrEqual(STABILITY.MAX_SINGLE_SHIFT);
  }
}
