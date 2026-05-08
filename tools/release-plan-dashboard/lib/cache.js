const MAX_CACHE_ENTRIES = 5000;
const CACHE_TTL_MS = 60 * 60 * 1000; // 1 hour for release plans + basic PR status
const PR_DETAIL_CACHE_TTL_MS = 15 * 60 * 1000; // 15 minutes for on-demand SDK PR details

const cache = {
  releasePlans: { data: null, fetchedAt: null, updatedAt: 0, refreshing: false },
  prDetails: new Map(), // url -> { data, updatedAt }
  prStatuses: new Map(), // url -> { data, updatedAt }
};

// Evict oldest entries when a cache Map exceeds MAX_CACHE_ENTRIES
function evictOldest(map) {
  if (map.size <= MAX_CACHE_ENTRIES) return;
  const entries = [...map.entries()].sort((a, b) => (a[1].updatedAt || 0) - (b[1].updatedAt || 0));
  const toRemove = entries.slice(0, map.size - MAX_CACHE_ENTRIES);
  for (const [key] of toRemove) map.delete(key);
}

export { cache, evictOldest, CACHE_TTL_MS, PR_DETAIL_CACHE_TTL_MS, MAX_CACHE_ENTRIES };
