import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { RateLimiter } from "./rate-limit.js";

describe("RateLimiter", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("allows requests within the limit", () => {
    const limiter = new RateLimiter(60_000, 3);

    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(true);
  });

  it("blocks requests exceeding the limit", () => {
    const limiter = new RateLimiter(60_000, 2);

    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(false);
  });

  it("returns correct remaining count", () => {
    const limiter = new RateLimiter(60_000, 3);

    expect(limiter.check("ip1").remaining).toBe(2);
    expect(limiter.check("ip1").remaining).toBe(1);
    expect(limiter.check("ip1").remaining).toBe(0);
  });

  it("tracks keys independently", () => {
    const limiter = new RateLimiter(60_000, 1);

    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip2").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(false);
  });

  it("resets after the window elapses", () => {
    const limiter = new RateLimiter(60_000, 1);

    expect(limiter.check("ip1").allowed).toBe(true);
    expect(limiter.check("ip1").allowed).toBe(false);

    vi.advanceTimersByTime(60_001);

    expect(limiter.check("ip1").allowed).toBe(true);
  });

  it("cleans up stale entries", () => {
    const limiter = new RateLimiter(1_000, 100);

    limiter.check("ip1");
    limiter.check("ip2");

    vi.advanceTimersByTime(1_001);

    // Trigger cleanup via a new check
    limiter.check("ip3");

    // ip1 should be allowed again (stale entry cleaned)
    const result = limiter.check("ip1");
    expect(result.allowed).toBe(true);
    expect(result.remaining).toBe(99);
  });
});
