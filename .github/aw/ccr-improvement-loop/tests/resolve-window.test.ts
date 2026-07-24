/**
 * resolve-window.test.ts — Gate 1 resolver identity.
 *
 * The cross-run cache key is `ccr-cache-${windowId}-`, and `windowId` comes from
 * this resolver. These tests pin the two properties that make the cache correct:
 * (1) different trigger SHAPES that describe the same (repo, window, cohort)
 * resolve to the SAME `windowId` (so a scheduled run and an equivalent dispatch
 * share a cache), and (2) any identity-bearing change (raw schema version,
 * cohort, bounds) yields a DIFFERENT `windowId` (so a stale cache can never be
 * silently restored).
 */
import { describe, expect, it } from "vitest";

import { resolveWindow } from "../scripts/resolve-window.ts";
import { RAW_SCHEMA_VERSION } from "../scripts/run-schema.ts";

const REPO = "Azure/azure-sdk-for-go";
// A fixed clock so the rolling window is deterministic across shapes.
const NOW = Date.parse("2026-07-01T00:00:00Z");

describe("resolveWindow — trigger-shape identity", () => {
    it("schedule (blank inputs) and dispatch (explicit 0/blank) share one windowId", () => {
        // Weekly schedule: env vars arrive as empty strings, cap unset.
        const schedule = resolveWindow({
            repo: REPO,
            windowStart: "",
            windowEnd: "",
            maxPrs: "",
            now: NOW,
        });
        // Manual dispatch of the same window: undefined bounds, explicit 0 cap.
        const dispatch = resolveWindow({
            repo: REPO,
            windowStart: undefined,
            windowEnd: undefined,
            maxPrs: "0",
            now: NOW,
        });
        // workflow_call: numeric 0 cap, omitted bounds.
        const call = resolveWindow({ repo: REPO, maxPrs: 0, now: NOW });

        expect(schedule.windowId).toBe(dispatch.windowId);
        expect(dispatch.windowId).toBe(call.windowId);
        expect(schedule.cohort).toBe("uncapped");
    });

    it("rolling window matches today − settleDays with start = end − windowDays", () => {
        const r = resolveWindow({ repo: REPO, now: NOW });
        // 2026-07-01 − 14d settle cutoff.
        expect(r.windowEnd).toBe("2026-06-17");
        // end − 14d window length.
        expect(r.windowStart).toBe("2026-06-03");
        expect(r.settleDays).toBe(14);
        expect(r.windowDays).toBe(14);
    });

    it("honors an explicit backfill window verbatim", () => {
        const r = resolveWindow({
            repo: REPO,
            windowStart: "2026-01-01",
            windowEnd: "2026-01-31",
            now: NOW,
        });
        expect(r.windowStart).toBe("2026-01-01");
        expect(r.windowEnd).toBe("2026-01-31");
        expect(r.windowId).toContain("__2026-01-01__2026-01-31__");
    });

    it("max_prs empty/0/negative all resolve to the uncapped cohort", () => {
        for (const maxPrs of ["", "0", 0, -3, null, undefined]) {
            const r = resolveWindow({ repo: REPO, maxPrs, now: NOW });
            expect(r.cohort).toBe("uncapped");
            expect(r.maxPrs).toBe(0);
        }
    });

    it("a positive max_prs yields a distinct capped cohort and windowId", () => {
        const uncapped = resolveWindow({ repo: REPO, now: NOW });
        const capped = resolveWindow({ repo: REPO, maxPrs: "50", now: NOW });
        expect(capped.cohort).toBe("cap-50");
        expect(capped.maxPrs).toBe(50);
        expect(capped.windowId).not.toBe(uncapped.windowId);
    });
});

describe("resolveWindow — key invalidation", () => {
    it("bumping the raw schema version changes the windowId (forces re-fetch)", () => {
        const current = resolveWindow({ repo: REPO, now: NOW });
        const bumped = resolveWindow({
            repo: REPO,
            now: NOW,
            rawSchemaVersion: `${RAW_SCHEMA_VERSION}-next`,
        });
        expect(bumped.windowId).not.toBe(current.windowId);
        expect(current.windowId).toContain(`__raw${RAW_SCHEMA_VERSION}`);
    });

    it("changing the cohort changes the windowId", () => {
        const uncapped = resolveWindow({ repo: REPO, now: NOW });
        const capped = resolveWindow({ repo: REPO, maxPrs: 25, now: NOW });
        expect(capped.windowId).not.toBe(uncapped.windowId);
    });

    it("changing repo or either bound changes the windowId", () => {
        const base = resolveWindow({
            repo: REPO,
            windowStart: "2026-01-01",
            windowEnd: "2026-01-31",
            now: NOW,
        });
        const otherRepo = resolveWindow({
            repo: "Azure/azure-sdk-for-net",
            windowStart: "2026-01-01",
            windowEnd: "2026-01-31",
            now: NOW,
        });
        const otherEnd = resolveWindow({
            repo: REPO,
            windowStart: "2026-01-01",
            windowEnd: "2026-02-01",
            now: NOW,
        });
        expect(otherRepo.windowId).not.toBe(base.windowId);
        expect(otherEnd.windowId).not.toBe(base.windowId);
    });
});
