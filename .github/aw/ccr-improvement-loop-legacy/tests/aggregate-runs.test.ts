import { describe, expect, it } from "vitest";

import { aggregate, dedupeRuns } from "../scripts/aggregate-runs.ts";
import { buildRunJson } from "../scripts/emit-run-json.ts";
import type { BuildRunInput, RunMetaInput } from "../scripts/emit-run-json.ts";
import type { PrRowOut } from "../scripts/pr-metrics.ts";
import type { AttributedComment } from "../scripts/types.ts";
import type { RunJson } from "../scripts/run-schema.ts";

function meta(repo: string, windowEnd = "2026-06-18"): RunMetaInput {
    return {
        repo,
        windowStart: "2026-06-01",
        windowEnd,
        windowLagDays: 14,
        prState: "merged",
        model: "openai/gpt-4o",
        modelTool: "gh models",
        temperature: 0,
        matchedCcrLogin: "ccr[bot]",
        promptHashes: {},
        vocabularyHash: null,
        toolVersion: "1.0",
        ccrEnabledSince: null,
    };
}

function prRow(number: number, ccrReviewed: boolean): PrRowOut {
    return {
        number,
        url: `https://x/pr/${String(number)}`,
        title: `pr ${String(number)}`,
        author: "alice",
        additions: 10,
        deletions: 2,
        createdAt: "2026-06-10T00:00:00Z",
        mergedAt: "2026-06-11T00:00:00Z",
        prType: "bug-fix",
        prTypeSource: "label",
        classificationStatus: "complete",
        ccrReviewed,
        cycleTimeHours: 24,
        iterations: 1,
    };
}

let cid = 1;
function humanAsk(
    severity: "critical" | "substantive" | "nit",
    isMiss: boolean,
): AttributedComment {
    const id = cid++;
    return {
        pr: 1,
        externalId: id,
        url: undefined,
        rowId: `1:inline:${String(id)}`,
        findingId: `1:alice:src/a.ts:${String(id)}`,
        authorKind: "human",
        authorLogin: "alice",
        kind: "ask",
        source: "inline",
        path: "src/a.ts",
        lineStart: id,
        lineEnd: id,
        lineStale: false,
        createdAt: "2026-06-10T01:00:00Z",
        ccrSawCode: true,
        pathExcluded: false,
        isSubstantive: true,
        diffDetectable: true,
        severity,
        category: "error-handling",
        confidence: 0.9,
        judgeStatus: "ok",
        judgeError: null,
        ccrOutcome: null,
        ccrAddressedConcern: !isMiss,
        isGap: isMiss,
        theme: "error-handling",
        body: "x",
    };
}

function ccrComment(
    severity: "critical" | "substantive" | "nit",
    addressed: boolean,
): AttributedComment {
    const id = cid++;
    return {
        pr: 1,
        externalId: id,
        url: undefined,
        rowId: `1:inline:${String(id)}`,
        findingId: `1:ccr[bot]:src/a.ts:${String(id)}`,
        authorKind: "ccr",
        authorLogin: "ccr[bot]",
        kind: "ask",
        source: "inline",
        path: "src/a.ts",
        lineStart: id,
        lineEnd: id,
        lineStale: false,
        createdAt: "2026-06-10T01:00:00Z",
        ccrSawCode: false,
        pathExcluded: false,
        isSubstantive: true,
        diffDetectable: true,
        severity,
        category: "error-handling",
        confidence: 0.9,
        judgeStatus: "ok",
        judgeError: null,
        ccrOutcome: addressed ? "addressed" : "ignored",
        ccrAddressedConcern: null,
        isGap: null,
        theme: "error-handling",
        body: "x",
    };
}

function run(over: {
    repo?: string;
    generatedAt: string;
    windowEnd?: string;
    prs?: PrRowOut[];
    comments?: AttributedComment[];
}): RunJson {
    const input: BuildRunInput = {
        meta: meta(over.repo ?? "Azure/go", over.windowEnd),
        prs: over.prs ?? [prRow(1, true)],
        comments: over.comments ?? [],
        themes: [],
        proposedEdits: [],
        experiment: null,
        automationLogins: [],
        generatedAt: over.generatedAt,
    };
    return buildRunJson(input);
}

describe("dedupeRuns", () => {
    it("keeps the newest generatedAt for a duplicate run.id", () => {
        const older = run({ generatedAt: "2026-06-18T00:00:00Z" });
        const newer = run({ generatedAt: "2026-06-19T00:00:00Z" });
        // Same id (same repo/window) → supersede.
        expect(older.run.id).toBe(newer.run.id);
        const deduped = dedupeRuns([older, newer]);
        expect(deduped).toHaveLength(1);
        expect(deduped[0]?.run.generatedAt).toBe("2026-06-19T00:00:00Z");
    });
});

describe("aggregate — fixture math", () => {
    it("computes ccrRecallRate time series in windowEnd order", () => {
        // Run A/B each carry one substantive human ask on a CCR-reviewed PR.
        const a = run({
            repo: "Azure/go",
            generatedAt: "2026-06-10T00:00:00Z",
            windowEnd: "2026-06-10",
            comments: [humanAsk("substantive", true)],
        });
        const b = run({
            repo: "Azure/py",
            generatedAt: "2026-06-12T00:00:00Z",
            windowEnd: "2026-06-12",
            comments: [humanAsk("substantive", true)],
        });
        const agg = aggregate([b, a]);
        expect(agg.ccrRecallRateOverTime.map((p) => p.runId)).toEqual([
            a.run.id,
            b.run.id,
        ]);
        // Values mirror each run's own metric exactly.
        expect(agg.ccrRecallRateOverTime[0]?.value).toBe(
            a.metrics.rates.ccrRecallRate?.value ?? null,
        );
        expect(agg.dateSpan.earliest).toBe("2026-06-10");
        expect(agg.dateSpan.latest).toBe("2026-06-12");
    });

    it("reports addressedRate by severity slices over time", () => {
        const a = run({
            generatedAt: "2026-06-10T00:00:00Z",
            comments: [ccrComment("critical", true), ccrComment("nit", false)],
        });
        const agg = aggregate([a]);
        const point = agg.addressedRateBySeverityOverTime[0];
        expect(point?.bySeverity.critical).toBe(
            a.metrics.rates.addressedRate?.slices?.find(
                (s) => s.severity === "critical",
            )?.value ?? null,
        );
        // critical: 1 addressed of 1 → 1.0; nit: 0 of 1 → 0.
        expect(point?.bySeverity.critical).toBe(1);
        expect(point?.bySeverity.nit).toBe(0);
    });

    it("reports bugFixPrRate by repo using the latest run per repo", () => {
        const go = run({
            repo: "Azure/go",
            generatedAt: "2026-06-10T00:00:00Z",
            prs: [prRow(1, true)],
        });
        const py = run({
            repo: "Azure/py",
            generatedAt: "2026-06-11T00:00:00Z",
        });
        const agg = aggregate([go, py]);
        expect(agg.bugFixPrRateByRepo.map((r) => r.repo)).toEqual([
            "Azure/go",
            "Azure/py",
        ]);
    });

    it("accounts for scanned/skipped counts", () => {
        const a = run({ generatedAt: "2026-06-10T00:00:00Z" });
        const agg = aggregate([a], 2, 3);
        expect(agg.runsScanned).toBe(3);
        expect(agg.runsKept).toBe(1);
        expect(agg.runsSkipped).toBe(2);
    });
});
