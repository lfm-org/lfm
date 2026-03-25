/** Interaction acknowledgement budgets (ms) */
export const ACK_BUDGET = {
  /** Standard interactions: button clicks, selections */
  STANDARD: 200,
  /** Heavier transitions: route changes, panel swaps */
  HEAVY: 300,
} as const;

/** Flow completion budgets (ms) */
export const COMPLETION_BUDGET = {
  /** Fast transitions with local/cached data */
  FAST: 1_000,
  /** Network-backed updates with visible loading */
  NETWORK: 2_000,
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
