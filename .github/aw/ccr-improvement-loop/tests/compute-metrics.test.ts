import { describe, expect, it } from "vitest";

import { computeMetrics } from "../scripts/compute-metrics.ts";
import type { ComputeMetricsOpts } from "../scripts/compute-metrics.ts";
import type { PrRowOut } from "../scripts/pr-metrics.ts";
import type { AttributedComment } from "../scripts/types.ts";
import type { Metric } from "../scripts/run-schema.ts";

const OPTS: ComputeMetricsOpts = {
    ccrEnabledSince: null,
    automationLogins: [],
};

function pr(over: Partial<PrRowOut> & { number: number }): PrRowOut {
    return {
        url: `https://x/pr/${String(over.number)}`,
        title: `pr ${String(over.number)}`,
        author: "alice",
        additions: 1,
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

let nextId = 1;
function comment(over: Partial<AttributedComment>): AttributedComment {
    const id = nextId++;
    const prNum = over.pr ?? 1;
    const path = over.path ?? "src/a.ts";
    const login = over.authorLogin ?? "alice";
    const lineStart = over.lineStart ?? id;
    const lineEnd = over.lineEnd ?? id;
    return {
        pr: prNum,
        externalId: id,
        url: `https://x/c/${String(id)}`,
        rowId: over.rowId ?? `${String(prNum)}:inline:${String(id)}`,
        findingId:
            over.findingId ??
            `${String(prNum)}:${login}:${path}:${String(lineStart)}-${String(lineEnd)}`,
        authorKind: "human",
        authorLogin: login,
        kind: "ask",
        source: "inline",
        path,
        lineStart,
        lineEnd,
        lineStale: false,
        createdAt: "2026-01-01T01:00:00Z",
        ccrSawCode: true,
        pathExcluded: false,
        ccrOutcome: null,
        ccrAddressedConcern: null,
        isSubstantive: true,
        diffDetectable: true,
        severity: null,
        category: null,
        confidence: null,
        judgeStatus: "ok",
        judgeError: null,
        isGap: null,
        theme: null,
        body: "please fix",
        ...over,
    };
}

function ccr(over: Partial<AttributedComment>): AttributedComment {
    return comment({
        authorKind: "ccr",
        authorLogin: "copilot-pull-request-reviewer[bot]",
        severity: "substantive",
        ...over,
    });
}

function rate(
    metrics: ReturnType<typeof computeMetrics>,
    name: string,
): Metric {
    const m = metrics.rates[name];
    if (!m) throw new Error(`no rate ${name}`);
    return m;
}

function sliceSum(m: Metric, key: "numerator" | "denominator"): number {
    return (m.slices ?? []).reduce((acc, s) => acc + (s[key] ?? 0), 0);
}

describe("compute-metrics", () => {
    it("division-by-zero yields value null, not NaN (zero-CCR window)", () => {
        const prs = [pr({ number: 1, ccrReviewed: false })];
        const m = computeMetrics(prs, [], OPTS);
        const addressed = rate(m, "addressedRate");
        expect(addressed.denominator).toBe(0);
        expect(addressed.value).toBeNull();
    });

    it("computes Q2 outcome rates over decided CCR comments", () => {
        const prs = [pr({ number: 1 })];
        const comments = [
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                severity: "critical",
                lineStart: 10,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "rejected",
                severity: "nit",
                lineStart: 20,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "ignored",
                severity: "nit",
                lineStart: 30,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "unclear",
                severity: "substantive",
                lineStart: 40,
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        expect(rate(m, "addressedRate").value).toBeCloseTo(1 / 3);
        expect(rate(m, "rejectedRate").value).toBeCloseTo(1 / 3);
        expect(rate(m, "ignoredRate").value).toBeCloseTo(1 / 3);
        expect(m.counts.ccrComments).toBe(4);
        expect(m.counts.ccrDecided).toBe(3);
        expect(m.counts.ccrOutcomeUnclear).toBe(1);
    });

    it("excludes pathExcluded CCR comments from Q2 denominators", () => {
        const prs = [pr({ number: 1 })];
        const comments = [
            ccr({ pr: 1, ccrOutcome: "addressed", lineStart: 10 }),
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                pathExcluded: true,
                path: "gen/x.ts",
                lineStart: 20,
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        expect(rate(m, "addressedRate").denominator).toBe(1);
        expect(m.counts.ccrCommentsPathExcluded).toBe(1);
    });

    it("excludes severity:null rows from addressedRate and reports the count", () => {
        const prs = [pr({ number: 1 })];
        const comments = [
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                severity: "critical",
                lineStart: 10,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                severity: null,
                judgeStatus: "failed",
                lineStart: 20,
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        const addressed = rate(m, "addressedRate");
        expect(addressed.denominator).toBe(1);
        expect(addressed.value).toBeCloseTo(1);
        expect(m.counts.ccrCommentsSeverityNull).toBe(1);
        // no severity:null slice present
        expect((addressed.slices ?? []).every((s) => s.severity !== null)).toBe(
            true,
        );
    });

    it("severity slices reconcile to the roll-up", () => {
        const prs = [pr({ number: 1, prType: "bug-fix" })];
        const comments = [
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                severity: "critical",
                lineStart: 10,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "ignored",
                severity: "nit",
                lineStart: 20,
            }),
            ccr({
                pr: 1,
                ccrOutcome: "addressed",
                severity: "nit",
                lineStart: 30,
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        const addressed = rate(m, "addressedRate");
        expect(sliceSum(addressed, "numerator")).toBe(addressed.numerator);
        expect(sliceSum(addressed, "denominator")).toBe(addressed.denominator);
    });

    it("prType slices reconcile to the roll-up including a null cell", () => {
        const prs = [
            pr({ number: 1, prType: "bug-fix" }),
            pr({
                number: 2,
                prType: null,
                prTypeSource: "unknown",
                classificationStatus: "needs-agent",
            }),
        ];
        const comments = [
            ccr({ pr: 1, ccrOutcome: "addressed", lineStart: 10 }),
            ccr({ pr: 2, ccrOutcome: "ignored", lineStart: 20 }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        const addressed = rate(m, "addressedRate");
        expect(sliceSum(addressed, "numerator")).toBe(addressed.numerator);
        expect(sliceSum(addressed, "denominator")).toBe(addressed.denominator);
        expect((addressed.slices ?? []).some((s) => s.prType === null)).toBe(
            true,
        );
    });

    it("ask metrics count distinct findingIds (inline + summary restating one ask counts once)", () => {
        const prs = [pr({ number: 1 })];
        const shared = "1:alice:src/a.ts:5-5";
        const comments = [
            comment({
                pr: 1,
                findingId: shared,
                kind: "ask",
                source: "inline",
            }),
            comment({
                pr: 1,
                findingId: shared,
                kind: "ask",
                source: "review",
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        expect(m.counts.humanAsks).toBe(1);
        expect(rate(m, "humanCommentsPerPr").numerator).toBe(1);
    });

    it("ccrRecallRate: caught over judged substantive diff-detectable asks on CCR-reviewed PRs", () => {
        const prs = [pr({ number: 1 }), pr({ number: 2, ccrReviewed: false })];
        const comments = [
            comment({
                pr: 1,
                isSubstantive: true,
                diffDetectable: true,
                ccrAddressedConcern: true,
                lineStart: 1,
            }),
            comment({
                pr: 1,
                isSubstantive: true,
                diffDetectable: true,
                ccrAddressedConcern: false,
                lineStart: 2,
            }),
            comment({
                pr: 1,
                isSubstantive: true,
                diffDetectable: true,
                ccrAddressedConcern: null,
                lineStart: 3,
            }),
            comment({
                pr: 1,
                isSubstantive: false,
                diffDetectable: true,
                lineStart: 4,
            }),
            comment({
                pr: 2,
                isSubstantive: true,
                diffDetectable: true,
                ccrAddressedConcern: true,
                lineStart: 5,
            }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        const recall = rate(m, "ccrRecallRate");
        // Unjudged (null) ask abstains; the ask on the non-CCR-reviewed PR is
        // excluded by the PR-level gate.
        expect(recall.numerator).toBe(1);
        expect(recall.denominator).toBe(2);
        expect(recall.direction).toBe("up");
    });

    it("ccrCoverage excludes bot-authored and pre-enablement PRs", () => {
        const prs = [
            pr({ number: 1, ccrReviewed: true }),
            pr({ number: 2, ccrReviewed: false, author: "dependabot[bot]" }),
            pr({
                number: 3,
                ccrReviewed: false,
                createdAt: "2020-01-01T00:00:00Z",
                mergedAt: "2020-01-02T00:00:00Z",
            }),
        ];
        const m = computeMetrics(prs, [], {
            ccrEnabledSince: "2025-01-01T00:00:00Z",
            automationLogins: [],
        });
        const cov = rate(m, "ccrCoverage");
        expect(cov.denominator).toBe(1);
        expect(cov.value).toBeCloseTo(1);
        expect(m.counts.eligibleForCoverage).toBe(1);
    });

    it("computes bugFixPrRate from merged bug-fix PRs", () => {
        const prs = [
            pr({ number: 1, prType: "bug-fix" }),
            pr({ number: 2, prType: "feature" }),
        ];
        const m = computeMetrics(prs, [], OPTS);
        expect(rate(m, "bugFixPrRate").value).toBeCloseTo(1 / 2);
        expect(m.counts.bugFixPrs).toBe(1);
        // verified-miss tracing was removed; those rates no longer exist.
        expect(m.rates.verifiedMissRate).toBeUndefined();
        expect(m.rates.preventableBugRate).toBeUndefined();
    });

    it("flags criticalCatchRate as lowConfidence", () => {
        const m = computeMetrics([pr({ number: 1 })], [], OPTS);
        expect(rate(m, "criticalCatchRate").lowConfidence).toBe(true);
    });

    it("warns when a slice denominator is below 5", () => {
        const prs = [pr({ number: 1 })];
        const comments = [
            ccr({ pr: 1, ccrOutcome: "addressed", lineStart: 1 }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        expect(
            m.coverageWarnings.some((w) => w.startsWith("addressedRate")),
        ).toBe(true);
    });

    it("single PR type → roll-up equals the only cell", () => {
        const prs = [
            pr({ number: 1, prType: "bug-fix" }),
            pr({ number: 2, prType: "bug-fix" }),
        ];
        const comments = [
            ccr({ pr: 1, ccrOutcome: "addressed", lineStart: 1 }),
            ccr({ pr: 2, ccrOutcome: "ignored", lineStart: 2 }),
        ];
        const m = computeMetrics(prs, comments, OPTS);
        const addressed = rate(m, "addressedRate");
        expect(addressed.slices?.length).toBe(1);
        expect(addressed.slices?.[0]?.value).toBeCloseTo(
            addressed.value ?? NaN,
        );
    });
});
