/**
 * gen-backlog.test.ts — Gate 3 window expansion (Track B, Phase 3).
 *
 * The derived backlog is only trustworthy if expansion is (a) settled-only — a
 * just-closed-but-unsettled period is excluded and appears ONLY after it settles
 * — and (b) non-overlapping with the exact cadence spans the fetch search relies
 * on. These tests pin both, for all three granularities, against a fixed
 * (config, today) so the expansion is fully deterministic.
 */
import { describe, expect, it } from "vitest";

import { genBacklog } from "../scripts/gen-backlog.ts";
import {
    PacerConfigSchema,
    type PacerConfig,
} from "../scripts/pacer-schema.ts";
import { computeWindowId } from "../scripts/window-id.ts";

const REPO = "Azure/azure-sdk-for-go";
const DAY = 86_400_000;

function cfg(over: Partial<PacerConfig> = {}): PacerConfig {
    return PacerConfigSchema.parse({
        repos: [REPO],
        start_date: "2026-01-01",
        granularity: "month",
        max_prs: 0,
        ...over,
    });
}

/** epoch ms for a UTC date. */
function at(date: string): number {
    return Date.parse(`${date}T00:00:00Z`);
}

describe("genBacklog — monthly expansion + settling", () => {
    it("emits full calendar months with inclusive last-day ends", () => {
        // today = 2026-05-01, settle cutoff = 04-17: Jan/Feb/Mar settled, April
        // (ends 04-30) not yet.
        const windows = genBacklog(cfg(), { now: at("2026-05-01") });
        const spans = windows.map((w) => [w.windowStart, w.windowEnd]);
        expect(spans).toEqual([
            ["2026-01-01", "2026-01-31"],
            ["2026-02-01", "2026-02-28"],
            ["2026-03-01", "2026-03-31"],
        ]);
    });

    it("excludes a just-closed-but-unsettled month, then includes it after it settles", () => {
        // 2026-04-30 + 14d settle = must be ≤ (today − 14d). April settles on
        // 2026-05-14. On 2026-05-13 April is NOT yet emitted...
        const before = genBacklog(cfg(), { now: at("2026-05-13") }).map(
            (w) => w.windowEnd,
        );
        expect(before).not.toContain("2026-04-30");
        expect(before.at(-1)).toBe("2026-03-31");

        // ...and on 2026-05-14 (exactly settleDays after April's last day) it is.
        const after = genBacklog(cfg(), { now: at("2026-05-14") }).map(
            (w) => w.windowEnd,
        );
        expect(after).toContain("2026-04-30");
    });

    it("adjacent months never share a boundary day (no double-count)", () => {
        const windows = genBacklog(cfg(), { now: at("2026-06-01") });
        for (let i = 1; i < windows.length; i++) {
            const prevEnd = at(windows[i - 1]!.windowEnd);
            const thisStart = at(windows[i]!.windowStart);
            // next window starts the day AFTER the previous inclusive end.
            expect(thisStart - prevEnd).toBe(DAY);
        }
    });
});

describe("genBacklog — biweekly + weekly cadences", () => {
    it("biweekly windows are exactly 14 days, anchored, non-overlapping", () => {
        const windows = genBacklog(cfg({ granularity: "biweekly" }), {
            now: at("2026-03-01"),
        });
        expect(windows[0]!.windowStart).toBe("2026-01-01");
        expect(windows[0]!.windowEnd).toBe("2026-01-14"); // 14 inclusive days
        for (const w of windows) {
            // inclusive span is 13 days between endpoints (14 calendar days).
            expect((at(w.windowEnd) - at(w.windowStart)) / DAY).toBe(13);
        }
        for (let i = 1; i < windows.length; i++) {
            expect(
                at(windows[i]!.windowStart) - at(windows[i - 1]!.windowEnd),
            ).toBe(DAY);
        }
    });

    it("weekly windows are exactly 7 days, anchored on start_date", () => {
        const windows = genBacklog(cfg({ granularity: "week" }), {
            now: at("2026-02-15"),
        });
        expect(windows[0]!.windowStart).toBe("2026-01-01");
        expect(windows[0]!.windowEnd).toBe("2026-01-07");
        for (const w of windows) {
            expect((at(w.windowEnd) - at(w.windowStart)) / DAY).toBe(6);
        }
    });
});

describe("genBacklog — identity + cohort + multi-repo", () => {
    it("stamps each candidate with its canonical windowId", () => {
        const [w] = genBacklog(cfg(), { now: at("2026-03-01") });
        expect(w!.windowId).toBe(
            computeWindowId({
                repo: REPO,
                windowStart: "2026-01-01",
                windowEnd: "2026-01-31",
                cohort: "uncapped",
            }),
        );
    });

    it("max_prs > 0 yields a capped cohort in every windowId", () => {
        const windows = genBacklog(cfg({ max_prs: 50 }), {
            now: at("2026-03-01"),
        });
        for (const w of windows) {
            expect(w.cohort).toBe("cap-50");
            expect(w.maxPrs).toBe(50);
            expect(w.windowId).toContain("__cap-50__");
        }
    });

    it("expands repo-major: every repo gets the same settled period set", () => {
        const repos = [REPO, "Azure/azure-sdk-for-python"];
        const windows = genBacklog(cfg({ repos }), { now: at("2026-04-01") });
        const byRepo = new Map<string, string[]>();
        for (const w of windows) {
            const list = byRepo.get(w.repo) ?? [];
            list.push(w.windowEnd);
            byRepo.set(w.repo, list);
        }
        expect(byRepo.get(repos[0]!)).toEqual(byRepo.get(repos[1]!));
        expect([...byRepo.keys()]).toEqual(repos);
    });

    it("returns empty when nothing has settled yet", () => {
        // start_date's first month can't have settled this soon.
        const windows = genBacklog(cfg({ start_date: "2026-06-01" }), {
            now: at("2026-06-10"),
        });
        expect(windows).toEqual([]);
    });
});

describe("PacerConfigSchema — validation", () => {
    it("defaults granularity to month and max_prs to 0", () => {
        const c = PacerConfigSchema.parse({
            repos: [REPO],
            start_date: "2026-01-01",
        });
        expect(c.granularity).toBe("month");
        expect(c.max_prs).toBe(0);
    });

    it("rejects an unknown granularity", () => {
        expect(() =>
            PacerConfigSchema.parse({
                repos: [REPO],
                start_date: "2026-01-01",
                granularity: "daily",
            }),
        ).toThrow();
    });

    it("rejects an invalid start_date and a bad repo", () => {
        expect(() =>
            PacerConfigSchema.parse({
                repos: [REPO],
                start_date: "2026-13-40",
            }),
        ).toThrow();
        expect(() =>
            PacerConfigSchema.parse({
                repos: ["not-a-repo"],
                start_date: "2026-01-01",
            }),
        ).toThrow();
    });
});
