import { describe, it, expect, vi, afterEach } from "vitest";
import { MemoryCache } from "./memory-cache.js";

afterEach(() => {
  vi.useRealTimers();
});

describe("MemoryCache", () => {
  describe("get / set", () => {
    it("returns undefined for missing key", () => {
      const cache = new MemoryCache<string>(1000);
      expect(cache.get("missing")).toBeUndefined();
    });

    it("returns value immediately after set", () => {
      const cache = new MemoryCache<string>(1000);
      cache.set("k", "hello");
      expect(cache.get("k")).toBe("hello");
    });

    it("stores and retrieves object values", () => {
      const cache = new MemoryCache<{ x: number }>(1000);
      const val = { x: 42 };
      cache.set("obj", val);
      expect(cache.get("obj")).toBe(val);
    });
  });

  describe("TTL expiry", () => {
    it("returns undefined after TTL has elapsed", () => {
      vi.useFakeTimers();
      const cache = new MemoryCache<string>(500);
      cache.set("k", "value");

      vi.advanceTimersByTime(501);
      expect(cache.get("k")).toBeUndefined();
    });

    it("returns value just before TTL boundary", () => {
      vi.useFakeTimers();
      const cache = new MemoryCache<string>(500);
      cache.set("k", "value");

      vi.advanceTimersByTime(499);
      expect(cache.get("k")).toBe("value");
    });

    it("removes expired entry from internal map on get", () => {
      vi.useFakeTimers();
      const cache = new MemoryCache<string>(100);
      cache.set("k", "value");
      vi.advanceTimersByTime(101);

      // First get evicts
      expect(cache.get("k")).toBeUndefined();
      // Second get still undefined (not a ghost)
      expect(cache.get("k")).toBeUndefined();
    });
  });

  describe("delete", () => {
    it("removes a key", () => {
      const cache = new MemoryCache<number>(5000);
      cache.set("a", 1);
      cache.delete("a");
      expect(cache.get("a")).toBeUndefined();
    });

    it("is a no-op for non-existent keys", () => {
      const cache = new MemoryCache<number>(5000);
      expect(() => cache.delete("nope")).not.toThrow();
    });
  });

  describe("clear", () => {
    it("removes all entries", () => {
      const cache = new MemoryCache<string>(5000);
      cache.set("a", "1");
      cache.set("b", "2");
      cache.set("c", "3");
      cache.clear();
      expect(cache.get("a")).toBeUndefined();
      expect(cache.get("b")).toBeUndefined();
      expect(cache.get("c")).toBeUndefined();
    });

    it("allows new entries after clear", () => {
      const cache = new MemoryCache<string>(5000);
      cache.set("x", "old");
      cache.clear();
      cache.set("x", "new");
      expect(cache.get("x")).toBe("new");
    });
  });
});
