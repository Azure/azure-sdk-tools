/**
 * compute-metrics.ts — pure arithmetic over attributed comments + PR rows.
 *
 * Produces the metric block in a single pass over already-judged,
 * already-traced rows. Design rules pinned here (and asserted in tests):
 *
 * - Every rate carries {numerator, denominator, value, denominatorDef, direction}
 *   plus a `slices` array. `value` is `null` (never NaN) when the denominator is 0.
 * - Ask-based counts use **distinct findingIds**, not raw comment counts, so a
 *   single ask restated inline + in a summary (same anchor) counts once.
 * - Severity-sliced rates exclude `severity: null` rows from BOTH the roll-up and
 *   the slices, so slice sums reconcile exactly to the roll-up; the excluded count
 *   is reported in `counts`. prType-only rates keep a `prType: null` cell so they
 *   reconcile over every row.
 * - `criticalCatchRate` is flagged `lowConfidence: true` in V1.
 *
 * The function is invoked once in the live pipeline, after judge (Step 3) and
 * trace (Step 4); here it is unit-tested against severity-bearing fixtures.
 */
import type {
    AttributedComment,
    CcrOutcome,
    PrType,
    Severity,
    VerifiedMiss,
} from "./types.ts";
import type { Metric, Metrics } from "./run-schema.ts";
import type { PrRowOut } from "./pr-metrics.ts";

export interface ComputeMetricsOpts {
    /** ISO date CCR was enabled; PRs created/merged before are coverage-ineligible. */
    ccrEnabledSince: string | null;
    /** Logins treated as automation/bots (PRs they author are coverage-ineligible). */
    automationLogins: string[];
}

type Dir = "up" | "down";

interface RateItem {
    prType: PrType | null;
    severity: Severity | null;
    inNum: boolean;
    inDen: boolean;
}

interface SliceKey {
    prType: PrType | null;
    severity: Severity | null;
}

const COVERAGE_MIN = 5;

function ratio(num: number, den: number): number | null {
    return den === 0 ? null : num / den;
}

function median(values: number[]): number | null {
    if (values.length === 0) return null;
    const sorted = [...values].sort((a, b) => a - b);
    const mid = Math.floor(sorted.length / 2);
    const lo = sorted[mid - 1];
    const hi = sorted[mid];
    if (sorted.length % 2 === 0) {
        if (lo === undefined || hi === undefined) return null;
        return (lo + hi) / 2;
    }
    return hi ?? null;
}

function sliceKeyOf(item: RateItem, bySeverity: boolean): string {
    const sev = bySeverity ? (item.severity ?? "null") : "null";
    return `${item.prType ?? "null"}::${sev}`;
}

/**
 * Build a rate Metric from per-item contributions. When `bySeverity` is false
 * the metric is sliced by prType only (a `prType: null` cell is allowed) so the
 * slices reconcile to the roll-up over every item. When `bySeverity` is true,
 * items with `severity: null` must already be filtered out by the caller.
 */
function buildRate(
    items: RateItem[],
    denominatorDef: string,
    direction: Dir,
    bySeverity: boolean,
    warnings: string[],
    metricName: string,
    lowConfidence = false,
): Metric {
    let num = 0;
    let den = 0;
    const cells = new Map<
        string,
        { key: SliceKey; num: number; den: number }
    >();
    for (const it of items) {
        if (it.inDen) den += 1;
        if (it.inNum) num += 1;
        const k = sliceKeyOf(it, bySeverity);
        let cell = cells.get(k);
        if (!cell) {
            cell = {
                key: {
                    prType: it.prType,
                    severity: bySeverity ? it.severity : null,
                },
                num: 0,
                den: 0,
            };
            cells.set(k, cell);
        }
        if (it.inDen) cell.den += 1;
        if (it.inNum) cell.num += 1;
    }

    const slices = [...cells.values()]
        .filter((c) => c.den > 0)
        .map((c) => ({
            prType: c.key.prType,
            severity: c.key.severity,
            numerator: c.num,
            denominator: c.den,
            value: ratio(c.num, c.den),
        }));

    for (const s of slices) {
        if (s.denominator > 0 && s.denominator < COVERAGE_MIN) {
            warnings.push(
                `${metricName}[${String(s.prType)}/${String(s.severity)}]: n=${String(
                    s.denominator,
                )} < ${String(COVERAGE_MIN)} (too small to trust)`,
            );
        }
    }
    if (den > 0 && den < COVERAGE_MIN) {
        warnings.push(
            `${metricName}: n=${String(den)} < ${String(COVERAGE_MIN)} (too small to trust)`,
        );
    }

    const metric: Metric = {
        numerator: num,
        denominator: den,
        value: ratio(num, den),
        denominatorDef,
        direction,
        slices,
    };
    if (lowConfidence) metric.lowConfidence = true;
    return metric;
}

/** Build a median Metric (value = median, slices = median per prType cell). */
function buildMedian(
    rows: { prType: PrType | null; value: number | null }[],
    denominatorDef: string,
    direction: Dir,
): Metric {
    const present = rows.filter(
        (r): r is { prType: PrType | null; value: number } => r.value != null,
    );
    const overall = median(present.map((r) => r.value));
    const byType = new Map<
        string,
        { prType: PrType | null; values: number[] }
    >();
    for (const r of present) {
        const k = r.prType ?? "null";
        let g = byType.get(k);
        if (!g) {
            g = { prType: r.prType, values: [] };
            byType.set(k, g);
        }
        g.values.push(r.value);
    }
    const slices = [...byType.values()].map((g) => ({
        prType: g.prType,
        severity: null,
        numerator: null,
        denominator: g.values.length,
        value: median(g.values),
    }));
    return {
        numerator: null,
        denominator: present.length,
        value: overall,
        denominatorDef,
        direction,
        slices,
    };
}

function isBotAuthor(
    author: string | null,
    automationLogins: string[],
): boolean {
    if (!author) return false;
    const lower = author.toLowerCase();
    if (lower.endsWith("[bot]")) return true;
    return automationLogins.some((l) => l.toLowerCase() === lower);
}

function isPostEnablement(pr: PrRowOut, since: string | null): boolean {
    if (!since) return true;
    const sinceMs = Date.parse(since);
    if (Number.isNaN(sinceMs)) return true;
    const ref = pr.mergedAt ?? pr.createdAt;
    if (!ref) return false;
    const refMs = Date.parse(ref);
    return !Number.isNaN(refMs) && refMs >= sinceMs;
}

/** Dedupe comment rows to one per findingId (first wins). */
function distinctByFindingId(
    comments: AttributedComment[],
): AttributedComment[] {
    const seen = new Set<string>();
    const out: AttributedComment[] = [];
    for (const c of comments) {
        if (seen.has(c.findingId)) continue;
        seen.add(c.findingId);
        out.push(c);
    }
    return out;
}

export function computeMetrics(
    prs: PrRowOut[],
    comments: AttributedComment[],
    verifiedMisses: VerifiedMiss[],
    opts: ComputeMetricsOpts,
): Metrics {
    const warnings: string[] = [];
    const prTypeByNumber = new Map<number, PrType | null>(
        prs.map((p) => [p.number, p.prType]),
    );
    const typeOf = (pr: number): PrType | null =>
        prTypeByNumber.get(pr) ?? null;

    const rates: Record<string, Metric> = {};

    // ---- Q1 --------------------------------------------------------------
    const humanAsks = distinctByFindingId(
        comments.filter((c) => c.authorKind === "human" && c.kind === "ask"),
    );
    const substantiveAsks = humanAsks.filter((c) => c.isSubstantive === true);

    // humanCommentsPerPr: distinct substantive human ask findingIds / PRs.
    // Expressed as an average; slices by prType. Denominator is the PR count
    // (overall) / per-type PR counts (slices) so the per-PR rate reconciles.
    {
        const prCountByType = new Map<string, number>();
        let prCount = 0;
        for (const p of prs) {
            prCount += 1;
            const k = p.prType ?? "null";
            prCountByType.set(k, (prCountByType.get(k) ?? 0) + 1);
        }
        const askCountByType = new Map<string, number>();
        let askCount = 0;
        for (const a of substantiveAsks) {
            askCount += 1;
            const k = typeOf(a.pr) ?? "null";
            askCountByType.set(k, (askCountByType.get(k) ?? 0) + 1);
        }
        const slices = [...prCountByType.entries()]
            .filter(([, den]) => den > 0)
            .map(([k, den]) => ({
                prType: k === "null" ? null : (k as PrType),
                severity: null,
                numerator: askCountByType.get(k) ?? 0,
                denominator: den,
                value: ratio(askCountByType.get(k) ?? 0, den),
            }));
        rates.humanCommentsPerPr = {
            numerator: askCount,
            denominator: prCount,
            value: ratio(askCount, prCount),
            denominatorDef:
                "distinct substantive human ask findingIds ÷ PRs in window",
            direction: "down",
            slices,
        };
    }

    rates.prCycleTime = buildMedian(
        prs.map((p) => ({ prType: p.prType, value: p.cycleTimeHours })),
        "median(mergedAt − createdAt) in hours over merged PRs",
        "down",
    );
    rates.iterationsPerPr = buildMedian(
        prs.map((p) => ({ prType: p.prType, value: p.iterations })),
        "median(commits after first human-or-CCR review event) over PRs",
        "down",
    );

    // ---- Q2 — CCR comment usefulness (judged outcome) -------------------
    // Outcome is an LLM verdict from the post-comment change + replies. Excluded
    // paths and severity-null rows are dropped from the severity-sliced rates so
    // slices reconcile; unclear/null outcomes are excluded from denominators and
    // reported in counts.
    const ccrAll = distinctByFindingId(
        comments.filter((c) => c.authorKind === "ccr"),
    );
    const ccrEligible = ccrAll.filter((c) => !c.pathExcluded);
    const ccrSeverityNull = ccrEligible.filter(
        (c) => c.severity == null,
    ).length;
    const ccrOutcomeNull = ccrEligible.filter(
        (c) => c.ccrOutcome == null,
    ).length;
    const ccrOutcomeUnclear = ccrEligible.filter(
        (c) => c.ccrOutcome === "unclear",
    ).length;

    // Definite-outcome, severity-known CCR comments — the shared denominator for
    // the three mutually-exclusive outcome rates.
    const decided = ccrEligible.filter(
        (c) =>
            c.severity != null &&
            (c.ccrOutcome === "addressed" ||
                c.ccrOutcome === "rejected" ||
                c.ccrOutcome === "ignored"),
    );
    const outcomeItems = (outcome: CcrOutcome): RateItem[] =>
        decided.map((c) => ({
            prType: typeOf(c.pr),
            severity: c.severity,
            inDen: true,
            inNum: c.ccrOutcome === outcome,
        }));

    rates.addressedRate = buildRate(
        outcomeItems("addressed"),
        "CCR comments the author addressed ÷ CCR comments with a definite outcome (severity-known)",
        "up",
        true,
        warnings,
        "addressedRate",
    );
    rates.rejectedRate = buildRate(
        outcomeItems("rejected"),
        "CCR comments the author rejected ÷ CCR comments with a definite outcome (severity-known)",
        "down",
        true,
        warnings,
        "rejectedRate",
    );
    rates.ignoredRate = buildRate(
        outcomeItems("ignored"),
        "CCR comments the author ignored ÷ CCR comments with a definite outcome (severity-known)",
        "down",
        true,
        warnings,
        "ignoredRate",
    );

    // ---- Cross-cutting ---------------------------------------------------
    const diffDetectableAsks = substantiveAsks.filter(
        (c) => c.diffDetectable === true,
    );
    // missRate: of substantive, diff-detectable human asks CCR *could* have seen
    // (ccrSawCode), the fraction CCR did not raise the same concern (isGap).
    const eligibleAsks = diffDetectableAsks.filter((c) => c.ccrSawCode);
    rates.missRate = buildRate(
        eligibleAsks.map((c) => ({
            prType: typeOf(c.pr),
            severity: null,
            inDen: true,
            inNum: c.isGap === true,
        })),
        "substantive ∧ diff-detectable human asks CCR could see that CCR did not raise ÷ those asks",
        "down",
        false,
        warnings,
        "missRate",
    );

    const coverageItems: RateItem[] = prs
        .filter(
            (p) =>
                !isBotAuthor(p.author, opts.automationLogins) &&
                isPostEnablement(p, opts.ccrEnabledSince),
        )
        .map((p) => ({
            prType: p.prType,
            severity: null,
            inDen: true,
            inNum: p.ccrReviewed,
        }));
    rates.ccrCoverage = buildRate(
        coverageItems,
        "PRs that received a CCR review ÷ eligible PRs (post-enablement, non-bot)",
        "up",
        false,
        warnings,
        "ccrCoverage",
    );

    // ---- Q3 — bug-verified track ----------------------------------------
    // bugFixPrRate is PR-based and computable now; the verified-miss rates are
    // null until trace-bug-origin.ts (Step 4) populates verifiedMisses.
    rates.bugFixPrRate = buildRate(
        prs.map((p) => ({
            prType: p.prType,
            severity: null,
            inDen: true,
            inNum: p.prType === "bug-fix",
        })),
        "bug-fix PRs ÷ all PRs in window",
        "down",
        false,
        warnings,
        "bugFixPrRate",
    );

    const tracedMisses = verifiedMisses.filter(
        (m) => m.traceOutcome === "resolved" && m.blameConfidence !== "low",
    );
    const bugFixPrs = prs.filter((p) => p.prType === "bug-fix").length;
    rates.verifiedMissRate = {
        numerator: tracedMisses.filter((m) => m.verifiedMiss).length,
        denominator: bugFixPrs,
        value: ratio(
            tracedMisses.filter((m) => m.verifiedMiss).length,
            bugFixPrs,
        ),
        denominatorDef: "verified misses ÷ successfully traced bug-fix PRs",
        direction: "down",
        slices: [],
    };
    const ccrActiveMisses = verifiedMisses.filter(
        (m) => m.ccrActiveOnIntroducingPr && m.blameConfidence !== "low",
    );
    rates.preventableBugRate = {
        numerator: ccrActiveMisses.filter((m) => m.verifiedMiss).length,
        denominator: ccrActiveMisses.length,
        value: ratio(
            ccrActiveMisses.filter((m) => m.verifiedMiss).length,
            ccrActiveMisses.length,
        ),
        denominatorDef:
            "verified misses where CCR was active ÷ introducing PRs with CCR active",
        direction: "down",
        slices: [],
    };
    {
        const criticalCcr = ccrEligible.filter(
            (c) => c.severity === "critical",
        );
        const known =
            criticalCcr.length +
            verifiedMisses.filter((m) => m.verifiedMiss).length;
        const caught = criticalCcr.filter(
            (c) => c.ccrOutcome === "addressed",
        ).length;
        rates.criticalCatchRate = {
            numerator: caught,
            denominator: known,
            value: ratio(caught, known),
            denominatorDef:
                "critical CCR comments acted on ÷ critical issues known (CCR + verified miss)",
            direction: "up",
            lowConfidence: true,
            slices: [],
        };
    }

    const counts: Record<string, number> = {
        prCount: prs.length,
        bugFixPrs,
        eligibleForCoverage: coverageItems.length,
        ccrReviewedPrs: prs.filter((p) => p.ccrReviewed).length,
        humanAsks: humanAsks.length,
        substantiveHumanAsks: substantiveAsks.length,
        diffDetectableAsks: diffDetectableAsks.length,
        eligibleAsks: eligibleAsks.length,
        gaps: eligibleAsks.filter((c) => c.isGap === true).length,
        ccrComments: ccrAll.length,
        ccrCommentsPathExcluded: ccrAll.length - ccrEligible.length,
        ccrCommentsSeverityNull: ccrSeverityNull,
        ccrOutcomeUnclear,
        ccrOutcomeNull,
        ccrDecided: decided.length,
        verifiedMisses: verifiedMisses.filter((m) => m.verifiedMiss).length,
    };

    return { rates, coverageWarnings: warnings, counts };
}
