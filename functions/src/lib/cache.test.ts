import { describe, it, expect } from "vitest";
import {
  isFresh,
  cooldownRemaining,
  ACCOUNT_CHARS_COOLDOWN_MS,
  CHARACTER_PROFILE_TTL_MS,
} from "./cache.js";

describe("isFresh", () => {
  it("returns false for undefined timestamp", () => {
    expect(isFresh(undefined, 1000)).toBe(false);
  });

  it("returns true when timestamp is within TTL", () => {
    const recent = new Date(Date.now() - 100).toISOString();
    expect(isFresh(recent, 1000)).toBe(true);
  });

  it("returns false when timestamp is older than TTL", () => {
    const old = new Date(Date.now() - 2000).toISOString();
    expect(isFresh(old, 1000)).toBe(false);
  });

  it("returns false when elapsed time equals TTL exactly", () => {
    const exact = new Date(Date.now() - 1000).toISOString();
    expect(isFresh(exact, 1000)).toBe(false);
  });
});

describe("cooldownRemaining", () => {
  it("returns 0 for undefined timestamp", () => {
    expect(cooldownRemaining(undefined, 1000)).toBe(0);
  });

  it("returns 0 when cooldown has expired", () => {
    const old = new Date(Date.now() - 2000).toISOString();
    expect(cooldownRemaining(old, 1000)).toBe(0);
  });

  it("returns positive seconds when within cooldown", () => {
    const recent = new Date(Date.now() - 100).toISOString();
    const remaining = cooldownRemaining(recent, 1000);
    expect(remaining).toBeGreaterThan(0);
    expect(remaining).toBeLessThanOrEqual(1);
  });
});

describe("TTL constants", () => {
  it("ACCOUNT_CHARS_COOLDOWN_MS is 15 minutes", () => {
    expect(ACCOUNT_CHARS_COOLDOWN_MS).toBe(15 * 60 * 1000);
  });

  it("CHARACTER_PROFILE_TTL_MS is 30 minutes", () => {
    expect(CHARACTER_PROFILE_TTL_MS).toBe(30 * 60 * 1000);
  });
});
