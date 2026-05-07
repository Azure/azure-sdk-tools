import { describe, test, expect } from "vitest";
import { cache, evictOldest, CACHE_TTL_MS, PR_DETAIL_CACHE_TTL_MS, MAX_CACHE_ENTRIES } from "../lib/cache.js";

describe("cache module", () => {
  describe("constants", () => {
    test("CACHE_TTL_MS is 1 hour", () => {
      expect(CACHE_TTL_MS).toBe(60 * 60 * 1000);
    });

    test("PR_DETAIL_CACHE_TTL_MS is 15 minutes", () => {
      expect(PR_DETAIL_CACHE_TTL_MS).toBe(15 * 60 * 1000);
    });

    test("MAX_CACHE_ENTRIES is 5000", () => {
      expect(MAX_CACHE_ENTRIES).toBe(5000);
    });
  });

  describe("cache structure", () => {
    test("has releasePlans object with required fields", () => {
      expect(cache.releasePlans).toBeDefined();
      expect(cache.releasePlans).toHaveProperty("data");
      expect(cache.releasePlans).toHaveProperty("fetchedAt");
      expect(cache.releasePlans).toHaveProperty("updatedAt");
      expect(cache.releasePlans).toHaveProperty("refreshing");
    });

    test("prDetails is a Map", () => {
      expect(cache.prDetails).toBeInstanceOf(Map);
    });

    test("prStatuses is a Map", () => {
      expect(cache.prStatuses).toBeInstanceOf(Map);
    });
  });

  describe("evictOldest", () => {
    test("does nothing when map is under limit", () => {
      const map = new Map();
      map.set("a", { updatedAt: 1 });
      map.set("b", { updatedAt: 2 });
      evictOldest(map);
      expect(map.size).toBe(2);
    });

    test("evicts oldest entries when over MAX_CACHE_ENTRIES", () => {
      const map = new Map();
      // Add MAX_CACHE_ENTRIES + 5 entries
      for (let i = 0; i < MAX_CACHE_ENTRIES + 5; i++) {
        map.set(`key-${i}`, { updatedAt: i });
      }
      expect(map.size).toBe(MAX_CACHE_ENTRIES + 5);
      evictOldest(map);
      expect(map.size).toBe(MAX_CACHE_ENTRIES);
      // Oldest 5 should be removed (updatedAt 0..4)
      expect(map.has("key-0")).toBe(false);
      expect(map.has("key-4")).toBe(false);
      // Newest should remain
      expect(map.has(`key-${MAX_CACHE_ENTRIES + 4}`)).toBe(true);
    });

    test("evicts entries with lowest updatedAt values", () => {
      const map = new Map();
      for (let i = 0; i < MAX_CACHE_ENTRIES + 3; i++) {
        // Insert in random order by using reversed updatedAt for even keys
        map.set(`k-${i}`, { updatedAt: i % 2 === 0 ? i : MAX_CACHE_ENTRIES * 2 - i });
      }
      evictOldest(map);
      expect(map.size).toBe(MAX_CACHE_ENTRIES);
    });

    test("handles entries with updatedAt of 0", () => {
      const map = new Map();
      for (let i = 0; i < MAX_CACHE_ENTRIES + 2; i++) {
        map.set(`k-${i}`, { updatedAt: i === 0 ? 0 : i + 100 });
      }
      evictOldest(map);
      expect(map.size).toBe(MAX_CACHE_ENTRIES);
      // Entry with updatedAt=0 should be evicted first
      expect(map.has("k-0")).toBe(false);
    });
  });
});
