/**
 * backfill-size-slices.test.ts — the historical size-slice backfill is only safe
 * because it refuses to write when its recompute disagrees with the file's stored
 * coverage (a mismatch would mean the size cells partition a different population
 * than the prType cells). These tests pin the three outcomes: a clean add, an
 * idempotent skip, and a guarded refusal.
 */
import { describe, expect, it } from "vitest";

import { planBackfill } from "../scripts/backfill-size-slices.ts";
import { computeMetrics } from "../scripts/compute-metrics.ts";
import type { PrRowOut } from "../scripts/pr-metrics.ts";
import type { Metric } from "../scripts/run-schema.ts";

function pr(over: Partial<PrRowOut> & { number: number }): PrRowOut {
    return {
        url: `https://x/pr/${String(over.number)}`,
        title: `pr ${String(over.number)}`,
        author: "alice",
        additions: 10,
        deletions: 0,
        createdAt: "2026-01-01T00:00:00Z",
        mergedAt: "2026-01-02T00:00:00Z",
        prType: "feature",
        prTypeSource: "label",
        classificationStatus: "complete",
        ccrReviewed: true,
        cycleTimeHours: 24,
        iterations: 1,
        ...over,
    };
}

const PRS: PrRowOut[] = [
    pr({ number: 1, additions: 6, deletions: 4, ccrReviewed: true }), // S
    pr({ number: 2, additions: 5, deletions: 5, ccrReviewed: false }), // S
    pr({ number: 3, additions: 500, deletions: 300, ccrReviewed: true }), // XL
];

/** A run object whose stored ccrCoverage matches PRS but has the size cells stripped. */
function runFileWithoutSizeCells(): {
    prs: PrRowOut[];
    comments: [];
    metrics: unknown;
    run: { ccrEnabledSince: null };
} {
    const m = computeMetrics(PRS, [], {
        ccrEnabledSince: null,
        automationLogins: [],
    });
    const cov = m.rates.ccrCoverage!;
    cov.slices = (cov.slices ?? []).filter((s) => s.sizeBucket == null);
    return {
        prs: PRS,
        comments: [],
        metrics: { rates: { ccrCoverage: cov } },
        run: { ccrEnabledSince: null },
    };
}

describe("planBackfill", () => {
    it("adds size cells when the recompute reproduces the stored coverage", () => {
        const run = runFileWithoutSizeCells();
        const res = planBackfill(run as never, []);
        expect(res.status).toBe("added");
        expect(res.sizeCells.map((c) => c.sizeBucket)).toEqual(["S", "XL"]);
        const s = res.sizeCells.find((c) => c.sizeBucket === "S");
        expect(s?.numerator).toBe(1);
        expect(s?.denominator).toBe(2);
    });

    it("is idempotent: skips a run that already carries size cells", () => {
        const run = runFileWithoutSizeCells();
        // Splice the size cells back in, as a prior backfill would have.
        const first = planBackfill(run as never, []);
        const cov = (run.metrics as { rates: { ccrCoverage: Metric } }).rates
            .ccrCoverage;
        cov.slices = [...(cov.slices ?? []), ...first.sizeCells];
        expect(planBackfill(run as never, []).status).toBe("already-present");
    });

    it("refuses (inconsistent) when the stored coverage disagrees with the recompute", () => {
        const run = runFileWithoutSizeCells();
        // Tamper the stored denominator so the eligible set can't be reproduced.
        const cov = (run.metrics as { rates: { ccrCoverage: Metric } }).rates
            .ccrCoverage;
        cov.denominator = 999;
        const res = planBackfill(run as never, []);
        expect(res.status).toBe("inconsistent");
        expect(res.sizeCells).toEqual([]);
    });
});
