/** Interaction acknowledgement budgets (ms) */
export const ACK_BUDGET = {
  /** Standard interactions: button clicks, selections */
  STANDARD: 200,
  /** Heavier transitions: route changes, panel swaps */
  HEAVY: 300,
  /** Cold entry loads: initial app shell render after navigation commit */
  ENTRY: 500,
} as const;

// Prefer distinct acknowledgement and completion markers. When the UI exposes
// no durable intermediate state on the local test backend, perf tests may
// intentionally collapse ack===completion and assert both against a completion
// budget instead of a stricter acknowledgement budget.

/** Flow completion budgets (ms) */
export const COMPLETION_BUDGET = {
  /** Fast transitions with local/cached data */
  FAST: 1_000,
  /** Network-backed updates with visible loading */
  NETWORK: 2_000,
  /** Full-page redirects that re-enter the app after server-side auth */
  REDIRECT: 2_500,
  /** Slower flows under mobile emulation */
  MOBILE: 3_000,
} as const;

/** Layout stability thresholds */
export const STABILITY = {
  /** Maximum cumulative layout shift during an interaction window */
  MAX_CLS: 0.1,
  /** Maximum single layout shift value */
  MAX_SINGLE_SHIFT: 0.05,
} as const;
