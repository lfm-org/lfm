export const ACCOUNT_CHARS_COOLDOWN_MS = 15 * 60 * 1000;
export const CHARACTER_PROFILE_TTL_MS = 30 * 60 * 1000;

/**
 * Returns true if timestamp is present and less than ttlMs old.
 */
export function isFresh(timestamp: string | undefined, ttlMs: number): boolean {
  if (!timestamp) return false;
  return Date.now() - new Date(timestamp).getTime() < ttlMs;
}

/**
 * Returns seconds remaining in cooldown, or 0 if expired/absent.
 */
export function cooldownRemaining(timestamp: string | undefined, ttlMs: number): number {
  if (!timestamp) return 0;
  const elapsed = Date.now() - new Date(timestamp).getTime();
  return Math.max(0, Math.ceil((ttlMs - elapsed) / 1000));
}
