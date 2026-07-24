/**
 * cache-persistence.test.ts — Gate 1 cross-run cache behavior, asserted against
 * the injected mock-gh recorder (tests/helpers/mock-gh.ts).
 *
 * `fetch-prs.ts` is the component that issues the per-PR `gh api` reads the
 * `actions/cache` step pair persists. These tests drive it through the recorder
 * to prove the two guarantees Gate 1 requires without a live token:
 *   - zero-refetch: a warm `.ccr-cache` yields ZERO `gh api` PR reads.
 *   - poison-cache: a mid-fetch failure writes NO `pr-<n>.json` (nothing to
 *     poison a retry with); the retry completes and a later run is all hits.
 *
 * The recorder built here is the same instrument Phases 2 and 4 reuse for their
 * request-count deltas.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, describe, expect, it } from "vitest";

import { setupMockGh, type MockGh } from "./helpers/mock-gh.ts";

const here = dirname(fileURLToPath(import.meta.url));
const fetchPrs = join(here, "..", "scripts", "fetch-prs.ts");
const REPO = "Azure/azure-sdk-for-go";

const cleanups: (() => void)[] = [];
afterEach(() => {
    while (cleanups.length > 0) cleanups.pop()?.();
});

function tmpCache(): string {
    const dir = mkdtempSync(path.join(os.tmpdir(), "ccr-cache-"));
    cleanups.push(() => {
        rmSync(dir, { recursive: true, force: true });
    });
    return dir;
}

function mockGh(opts?: Parameters<typeof setupMockGh>[0]): MockGh {
    const gh = setupMockGh(opts);
    cleanups.push(() => {
        gh.cleanup();
    });
    return gh;
}

/** Run fetch-prs for one explicit PR number; returns exit success. */
function fetchOne(gh: MockGh, cacheDir: string, n: number): { ok: boolean } {
    try {
        execFileSync(
            "node",
            [
                fetchPrs,
                "--repo",
                REPO,
                "--number",
                String(n),
                "--cache-dir",
                cacheDir,
                "--quiet",
            ],
            { env: { ...process.env, ...gh.env }, stdio: "ignore" },
        );
        return { ok: true };
    } catch {
        return { ok: false };
    }
}

describe("cross-run cache persistence", () => {
    it("second run over a warm cache issues zero gh api PR fetches", () => {
        const gh = mockGh();
        const cache = tmpCache();

        // Cold run populates the cache.
        expect(fetchOne(gh, cache, 100).ok).toBe(true);
        expect(existsSync(join(cache, "pr-100.json"))).toBe(true);
        expect(gh.apiCalls().length).toBeGreaterThan(0);

        // Warm run: same (repo, window, cohort) — every PR is a cache hit.
        gh.reset();
        expect(fetchOne(gh, cache, 100).ok).toBe(true);
        expect(gh.apiCalls().length).toBe(0);
    });

    it("a mid-fetch failure saves no cache entry; a retry completes and a later run is all hits", () => {
        const cache = tmpCache();

        // Poisoned run: the GraphQL base read is forced to fail, so
        // fetchPrToCache rejects before it ever writes pr-101.json.
        const failing = mockGh({ fail: ["query=# op:base"] });
        expect(fetchOne(failing, cache, 101).ok).toBe(false);
        expect(existsSync(join(cache, "pr-101.json"))).toBe(false);

        // Retry with a healthy gh: the raw fetch completes and the entry lands.
        const healthy = mockGh();
        expect(fetchOne(healthy, cache, 101).ok).toBe(true);
        expect(existsSync(join(cache, "pr-101.json"))).toBe(true);
        expect(healthy.apiCalls().length).toBeGreaterThan(0);

        // Subsequent run restores the completed cache and refetches nothing.
        healthy.reset();
        expect(fetchOne(healthy, cache, 101).ok).toBe(true);
        expect(healthy.apiCalls().length).toBe(0);
    });
});
