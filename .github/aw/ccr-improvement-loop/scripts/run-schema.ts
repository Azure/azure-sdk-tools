/**
 * run-schema.ts — the single source of truth for the persisted run JSON.
 *
 * Every artifact written to disk validates against this schema on write AND on
 * read. Fields are camelCase and match the TS types exactly; there is no
 * field-name conversion layer. `.strict()` rejects unknown/legacy field names so
 * schema drift is caught in CI (see run-schema.test.ts).
 */
import { z } from "zod";

// 1.1: `run.id` is now the canonical window identity (see window-id.ts) —
// `${owner}_${repo}__${windowStart}__${windowEnd}__${cohort}__raw${RAW_SCHEMA_VERSION}` —
// instead of the old `${windowEnd}_${owner}_${repo}`. The bump forces old-format
// run files to be migrated (scripts/migrate-run-schema.ts) rather than silently
// mixed with new-format ids under the dedupe/supersede rule.
export const SCHEMA_VERSION = "1.1";
export const RAW_SCHEMA_VERSION = "1.0";

const PrType = z.enum([
    "bug-fix",
    "feature",
    "refactor",
    "docs",
    "test",
    "chore",
]);
const PrTypeSource = z.enum(["label", "title", "issue", "agent", "unknown"]);
const ClassificationStatus = z.enum(["complete", "needs-agent", "failed"]);
const Severity = z.enum(["critical", "substantive", "nit"]);
const JudgeStatus = z.enum(["ok", "failed", "lowConfidence"]);
/**
 * PR-size bucket by total changed lines (additions + deletions). A deterministic
 * slice dimension over already-captured raw fields — no agent re-run. Used to
 * facet CCR coverage by PR size on the dashboard (D-scale polish).
 */
export const SizeBucket = z.enum(["S", "M", "L", "XL"]);
export type SizeBucketT = z.infer<typeof SizeBucket>;

/**
 * Bucket a PR by total changed lines. Returns `null` when the size is unknown
 * (either count null), so an unbucketable PR is excluded rather than mislabelled.
 * Thresholds: S &lt; 50, M &lt; 200, L &lt; 500, XL ≥ 500.
 */
export function sizeBucketOf(
    additions: number | null,
    deletions: number | null,
): SizeBucketT | null {
    if (additions == null || deletions == null) return null;
    const total = additions + deletions;
    if (total < 50) return "S";
    if (total < 200) return "M";
    if (total < 500) return "L";
    return "XL";
}
const CommentKind = z.enum(["ask", "reply", "summary"]);
const CommentSource = z.enum(["review", "inline", "issue"]);
const AuthorKind = z.enum(["human", "ccr", "bot"]);
const CcrOutcome = z.enum(["addressed", "rejected", "ignored", "unclear"]);
const Category = z.enum([
    "error-handling",
    "concurrency",
    "input-validation",
    "security",
    "resource-management",
    "api-design",
    "backward-compatibility",
    "type-safety",
    "performance",
    "testing",
    "logging-observability",
    "documentation",
    "style-naming",
    "configuration",
    "other",
]);

export const RunMetaSchema = z
    .object({
        id: z.string(),
        repo: z.string(),
        windowStart: z.string(),
        windowEnd: z.string(),
        windowLagDays: z.number(),
        prState: z.string(),
        prCount: z.number().int().nonnegative(),
        model: z.string(),
        modelTool: z.string(),
        temperature: z.number(),
        matchedCcrLogin: z.string().nullable(),
        promptHashes: z.record(z.string()),
        vocabularyHash: z.string().nullable(),
        toolVersion: z.string(),
        ccrEnabledSince: z.string().nullable(),
        /** ONLY volatile field — wall-clock UTC emit time. */
        generatedAt: z.string(),
    })
    .strict();

export const PrRowSchema = z
    .object({
        number: z.number().int(),
        url: z.string(),
        title: z.string(),
        author: z.string().nullable(),
        additions: z.number().int().nullable(),
        deletions: z.number().int().nullable(),
        createdAt: z.string().nullable(),
        mergedAt: z.string().nullable(),
        prType: PrType.nullable(),
        prTypeSource: PrTypeSource,
        classificationStatus: ClassificationStatus,
        ccrReviewed: z.boolean(),
        cycleTimeHours: z.number().nullable(),
        iterations: z.number().int().nonnegative(),
    })
    .strict();

export const CommentRowSchema = z
    .object({
        pr: z.number().int(),
        externalId: z.number().int(),
        url: z.string().nullable(),
        rowId: z.string(),
        findingId: z.string(),
        authorKind: AuthorKind,
        authorLogin: z.string().nullable(),
        kind: CommentKind,
        source: CommentSource,
        path: z.string().nullable(),
        lineStart: z.number().int().nullable(),
        lineEnd: z.number().int().nullable(),
        lineStale: z.boolean(),
        createdAt: z.string().nullable(),
        // judge output (null + judgeStatus when unjudged/failed)
        isSubstantive: z.boolean().nullable(),
        diffDetectable: z.boolean().nullable(),
        severity: Severity.nullable(),
        category: Category.nullable(),
        confidence: z.number().nullable(),
        judgeStatus: JudgeStatus.nullable(),
        judgeError: z.string().nullable(),
        // deterministic eligibility gate
        ccrSawCode: z.boolean(),
        pathExcluded: z.boolean(),
        // judge-filled verdicts
        ccrOutcome: CcrOutcome.nullable(),
        ccrAddressedConcern: z.boolean().nullable(),
        isGap: z.boolean().nullable(),
        theme: Category.nullable(),
    })
    .strict();

export const ThemeSchema = z
    .object({
        label: Category,
        gapCount: z.number().int().nonnegative(),
        askCount: z.number().int().nonnegative(),
        distinctReviewers: z.number().int().nonnegative(),
        promoted: z.boolean(),
        promotedVia: z.enum(["opinion", "evidence"]).nullable(),
        priorityScore: z.number(),
        sourcePrs: z.array(z.number().int()),
    })
    .strict();

export const ProposedEditSchema = z
    .object({
        file: z.string(),
        applyTo: z.string().nullable(),
        theme: Category,
        rule: z.string(),
        redundantWith: z.string().nullable(),
        sourcePrs: z.array(z.number().int()),
        provenance: z.string(),
        status: z.enum(["proposed", "applied", "blocked", "retired"]),
    })
    .strict();

/** A single normalized metric with numerator/denominator and slices. */
export const MetricSchema = z
    .object({
        numerator: z.number().nullable(),
        denominator: z.number().nullable(),
        value: z.number().nullable(),
        denominatorDef: z.string(),
        direction: z.enum(["up", "down"]),
        lowConfidence: z.boolean().optional(),
        slices: z
            .array(
                z
                    .object({
                        prType: PrType.nullable(),
                        severity: Severity.nullable(),
                        /**
                         * Optional third slice dimension (PR-size bucket). Present
                         * only on size-sliced cells; absent on prType/severity
                         * cells. Additive + optional so existing run files parse.
                         */
                        sizeBucket: SizeBucket.optional(),
                        numerator: z.number().nullable(),
                        denominator: z.number().nullable(),
                        value: z.number().nullable(),
                    })
                    .strict(),
            )
            .optional(),
    })
    .strict();

export const MetricsSchema = z
    .object({
        rates: z.record(MetricSchema),
        coverageWarnings: z.array(z.string()),
        counts: z.record(z.number()),
    })
    .strict();

export const ExperimentSchema = z
    .object({
        sourceThemes: z.array(Category),
        filesTouched: z.array(z.string()),
        sourcePrs: z.array(z.number().int()),
        replayPrSet: z.array(z.number().int()),
        replayPrCount: z.number().int().nonnegative(),
        missRateBefore: z.number().nullable(),
        missRateAfter: z.number().nullable(),
        benchmarkPassed: z.boolean().nullable(),
    })
    .strict();

export const RunSchema = z
    .object({
        schemaVersion: z.literal(SCHEMA_VERSION),
        run: RunMetaSchema,
        prs: z.array(PrRowSchema),
        comments: z.array(CommentRowSchema),
        themes: z.array(ThemeSchema),
        metrics: MetricsSchema,
        proposedEdits: z.array(ProposedEditSchema),
        experiment: ExperimentSchema.nullable(),
    })
    .strict();

export type RunJson = z.infer<typeof RunSchema>;
export type RunMeta = z.infer<typeof RunMetaSchema>;
export type PrRow = z.infer<typeof PrRowSchema>;
export type CommentRow = z.infer<typeof CommentRowSchema>;
export type Metric = z.infer<typeof MetricSchema>;
export type Metrics = z.infer<typeof MetricsSchema>;
export type Experiment = z.infer<typeof ExperimentSchema>;

/** Parse + validate a run JSON (throws on any drift). */
export function parseRun(value: unknown): RunJson {
    return RunSchema.parse(value);
}
