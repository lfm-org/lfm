import { HttpRequest } from "@azure/functions";
import { errorResponse } from "./security-headers.js";

interface SlidingWindowEntry {
  timestamps: number[];
}

/**
 * In-memory sliding window rate limiter.
 *
 * Suitable for Azure Functions Consumption plan (single instance).
 * State resets on cold starts — acceptable for a hobby project.
 */
export class RateLimiter {
  private readonly windowMs: number;
  private readonly maxRequests: number;
  private readonly store = new Map<string, SlidingWindowEntry>();
  private lastCleanup = Date.now();

  constructor(windowMs: number, maxRequests: number) {
    this.windowMs = windowMs;
    this.maxRequests = maxRequests;
  }

  check(key: string): { allowed: boolean; remaining: number } {
    const now = Date.now();
    this.maybeCleanup(now);

    const cutoff = now - this.windowMs;
    let entry = this.store.get(key);

    if (!entry) {
      entry = { timestamps: [] };
      this.store.set(key, entry);
    }

    // Remove timestamps outside the window
    entry.timestamps = entry.timestamps.filter((t) => t > cutoff);

    if (entry.timestamps.length >= this.maxRequests) {
      return { allowed: false, remaining: 0 };
    }

    entry.timestamps.push(now);
    return { allowed: true, remaining: this.maxRequests - entry.timestamps.length };
  }

  /** Periodically prune stale entries to prevent memory growth. */
  private maybeCleanup(now: number): void {
    if (now - this.lastCleanup < this.windowMs) return;
    this.lastCleanup = now;

    const cutoff = now - this.windowMs;
    for (const [key, entry] of this.store) {
      entry.timestamps = entry.timestamps.filter((t) => t > cutoff);
      if (entry.timestamps.length === 0) this.store.delete(key);
    }
  }
}

export function getClientIp(request: HttpRequest): string {
  return (
    request.headers.get("x-forwarded-for")?.split(",")[0].trim() ||
    request.headers.get("x-real-ip") ||
    "unknown"
  );
}

// Pre-configured limiters for different endpoint tiers.
// Standard: 60 requests per minute per IP.
export const standardLimiter = new RateLimiter(60_000, 60);
// Auth: 10 requests per minute per IP (login/callback are sensitive).
export const authLimiter = new RateLimiter(60_000, 10);
// Write: 20 requests per minute per IP (create/update/delete operations).
export const writeLimiter = new RateLimiter(60_000, 20);

export function rateLimitResponse() {
  return errorResponse(429, "Too many requests");
}
