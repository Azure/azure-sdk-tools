/**
 * pacer-schema.ts — the load-bearing zod schemas for Track B (the pacer).
 *
 * Four persisted shapes are validated on every read AND write so drift is caught
 * in CI rather than at 3am in a cron run:
 *
 *  - {@link PacerConfigSchema}   — `pacer/config.json`, the backlog source.
 *  - {@link OutcomeArtifactSchema} — `outcome-<window_id>.json`, the terminal
 *    outcome a loop run uploads (the pacer's completion signal).
 *  - {@link LedgerRecordSchema}  — `ledger/<window_id>.json` on the durable
 *    `ccr-pacer-state` branch: one record per terminal window.
 *  - {@link SkippedSchema}       — `skipped.json`: poison windows retired after
 *    the retry cap.
 *
 * All object schemas are `.strict()` — unknown/legacy keys are a hard error, so a
 * renamed field can never be silently dropped. Every record is keyed by the
 * canonical `window_id` (see window-id.ts), which is what makes completion and
 * idempotency well-defined across the whole loop.
 */
import { z } from "zod";

/** Terminal outcomes. `produced` and the two `*-noop`s are DONE; `failed` retries. */
export const OUTCOMES = [
    "produced",
    "thin-noop",
    "signal-noop",
    "failed",
] as const;
export const OutcomeSchema = z.enum(OUTCOMES);
export type Outcome = (typeof OUTCOMES)[number];

/** The done outcomes — a window with any of these needs no further work. */
export const DONE_OUTCOMES: readonly Outcome[] = [
    "produced",
    "thin-noop",
    "signal-noop",
];

/** Backlog expansion granularity. Monthly is the default. */
export const GRANULARITIES = ["month", "biweekly", "week"] as const;
export const GranularitySchema = z.enum(GRANULARITIES);
export type Granularity = (typeof GRANULARITIES)[number];

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const OWNER_REPO = /^[^/\s]+\/[^/\s]+$/;

/**
 * Static per-window cost-estimate defaults + admission knobs. Exported so the
 * child loop's C2 preflight guard (prep-run.ts) and the pacer admission (pace.ts)
 * share ONE source of truth — a conservative upper bound for a high-volume
 * uncapped month, never a value learned from observed spend.
 */
export const DEFAULT_EST_REST_CALLS_PER_WINDOW = 1200;
export const DEFAULT_EST_GRAPHQL_POINTS_PER_WINDOW = 2000;
export const DEFAULT_EST_SEARCH_CALLS_PER_WINDOW = 10;
export const DEFAULT_SAFETY_MARGIN = 500;
export const DEFAULT_MAX_WINDOWS_PER_TICK = 4;
export const DEFAULT_RETRY_CAP = 1;

/** A YYYY-MM-DD string that also parses to a real calendar date. */
const isoDateString = z
    .string()
    .regex(ISO_DATE, "must be YYYY-MM-DD")
    .refine((s) => !Number.isNaN(Date.parse(`${s}T00:00:00Z`)), {
        message: "must be a valid calendar date",
    });

/**
 * `pacer/config.json`. The backlog is derived from this + today's date + the
 * durable ledger — there is no hand-maintained status file.
 */
export const PacerConfigSchema = z
    .object({
        /** Target repos, each "owner/name". */
        repos: z
            .array(z.string().regex(OWNER_REPO, "must be owner/name"))
            .min(1),
        /** Backfill anchor; windows are expanded forward from here. */
        start_date: isoDateString,
        /** Expansion cadence; monthly by default. */
        granularity: GranularitySchema.default("month"),
        /** Per-window PR cap; `0` (default) = uncapped full cohort. */
        max_prs: z.number().int().nonnegative().default(0),
        /**
         * Static conservative upper bound of core-REST calls a single window
         * costs. NOT an estimate learned from observed spend — a fixed constant
         * (Phase 4) chosen to cover a high-volume uncapped month with margin. The
         * pacer admits a window only if this fits the remaining budget; it never
         * probes counts. Default 1200 (< a fresh 5000/hr core hour minus margin).
         */
        est_rest_calls_per_window: z
            .number()
            .int()
            .positive()
            .default(DEFAULT_EST_REST_CALLS_PER_WINDOW),
        /**
         * Static conservative upper bound of GraphQL points a single window costs.
         * Metered for hygiene, not because points bind (a whole month of per-PR
         * fixed reads is only a few hundred points against a 5k/hr pool). Default
         * 2000.
         */
        est_graphql_points_per_window: z
            .number()
            .int()
            .positive()
            .default(DEFAULT_EST_GRAPHQL_POINTS_PER_WINDOW),
        /**
         * Static conservative upper bound of `search` calls a single window costs
         * (the 30/min pool). Checked by the C2 preflight guard. Default 10.
         */
        est_search_calls_per_window: z
            .number()
            .int()
            .positive()
            .default(DEFAULT_EST_SEARCH_CALLS_PER_WINDOW),
        /**
         * Headroom (per pool) always kept free so admission never rides the ceiling
         * and a concurrent unrelated job can still transact. Subtracted from both
         * the per-tick `remaining` and the fresh-hour `limit` in every fit check.
         */
        safety_margin: z
            .number()
            .int()
            .nonnegative()
            .default(DEFAULT_SAFETY_MARGIN),
        /**
         * Hard cap on windows admitted per tick — the real bound on aggregate
         * cross-repo GraphQL exposure (see plan Phase 4). Deferred windows resume
         * next tick (the cache makes re-dispatch free). Default 4.
         */
        max_windows_per_tick: z
            .number()
            .int()
            .positive()
            .default(DEFAULT_MAX_WINDOWS_PER_TICK),
        /**
         * How many times a `failed` window is retried before it is retired to
         * `skipped.json`. `1` (default) = one retry: attempt 1 fails → retry as
         * attempt 2 → if that fails too, skip (no poison loop).
         */
        retry_cap: z.number().int().nonnegative().default(DEFAULT_RETRY_CAP),
    })
    .strict();
export type PacerConfig = z.infer<typeof PacerConfigSchema>;

/** One metered rate-limit pool: its hourly ceiling and current remaining. */
export const PoolBudgetSchema = z
    .object({
        /** The pool's full hourly ceiling (used for the fresh-hour fit check). */
        limit: z.number().int().nonnegative(),
        /** Currently remaining in this tick's hour. */
        remaining: z.number().int().nonnegative(),
    })
    .strict();
export type PoolBudget = z.infer<typeof PoolBudgetSchema>;

/**
 * The metered pools the pacer admits against. `core` REST is the pool that binds
 * in practice (the `1 + N` per-PR primary constraint); `graphql` and `search` are
 * metered for hygiene. Parsed from `gh api rate_limit`.
 */
export const BudgetSchema = z
    .object({
        core: PoolBudgetSchema,
        graphql: PoolBudgetSchema,
        search: PoolBudgetSchema,
    })
    .strict();
export type Budget = z.infer<typeof BudgetSchema>;

/**
 * The observed budget spend for a window, logged for `--dry-run` visibility only
 * — it is NEVER fed back into an estimator (the pacer uses a static conservative
 * check; see deferred-and-scope.md).
 */
export const CostSchema = z
    .object({
        /** Core REST calls consumed (the `1 + N` primary constraint). */
        rest: z.number().nonnegative(),
        /** GraphQL points consumed (hygiene meter, not usually binding). */
        graphql: z.number().nonnegative(),
    })
    .strict();
export type Cost = z.infer<typeof CostSchema>;

/**
 * `outcome-<window_id>.json` — the deterministic terminal signal a loop run
 * uploads. `artifact_name` is derived from `window_id` (`run-<window_id>`), never
 * read from an unstable upload temp id, so the pacer can correlate it offline.
 */
export const OutcomeArtifactSchema = z
    .object({
        window_id: z.string().min(1),
        outcome: OutcomeSchema,
        /** Deterministic run-artifact basename (`run-<window_id>`). */
        artifact_name: z.string().min(1),
        /** GitHub Actions run id (string form). */
        run_id: z.string().min(1),
        /** GitHub Actions run attempt (1-based). */
        attempt: z.number().int().positive(),
        /** ISO-8601 emit timestamp. */
        ts: z.string().min(1),
        cost: CostSchema,
    })
    .strict();
export type OutcomeArtifact = z.infer<typeof OutcomeArtifactSchema>;

/**
 * `ledger/<window_id>.json` on the `ccr-pacer-state` branch — the single durable
 * source of truth for window completion. Re-writing the same terminal record is a
 * no-op (idempotent), so appends across ticks never collide.
 */
export const LedgerRecordSchema = z
    .object({
        window_id: z.string().min(1),
        repo: z.string().regex(OWNER_REPO),
        window_start: isoDateString,
        window_end: isoDateString,
        /** "uncapped" | "cap-<N>". */
        cohort: z.string().min(1),
        /** Run-JSON schema version at write time. */
        schema_version: z.string().min(1),
        outcome: OutcomeSchema,
        attempt: z.number().int().positive(),
        run_id: z.string().min(1),
        artifact_name: z.string().min(1),
        ts: z.string().min(1),
    })
    .strict();
export type LedgerRecord = z.infer<typeof LedgerRecordSchema>;

/** One retired poison window. */
export const SkippedWindowSchema = z
    .object({
        window_id: z.string().min(1),
        /** Why it was retired (e.g. "exceeded retry cap after 3 failed runs"). */
        reason: z.string().min(1),
        ts: z.string().min(1),
    })
    .strict();
export type SkippedWindow = z.infer<typeof SkippedWindowSchema>;

/** `skipped.json` — the set of windows retired after the retry cap. */
export const SkippedSchema = z
    .object({
        windows: z.array(SkippedWindowSchema),
    })
    .strict();
export type Skipped = z.infer<typeof SkippedSchema>;

/** Parse + validate a pacer config (throws on any drift). */
export function parsePacerConfig(value: unknown): PacerConfig {
    return PacerConfigSchema.parse(value);
}

/** Parse + validate an outcome artifact (throws on any drift). */
export function parseOutcomeArtifact(value: unknown): OutcomeArtifact {
    return OutcomeArtifactSchema.parse(value);
}

/** Parse + validate a ledger record (throws on any drift). */
export function parseLedgerRecord(value: unknown): LedgerRecord {
    return LedgerRecordSchema.parse(value);
}

/** Parse + validate a skipped.json document (throws on any drift). */
export function parseSkipped(value: unknown): Skipped {
    return SkippedSchema.parse(value);
}

/**
 * Extract the metered {@link Budget} from a raw `gh api rate_limit` response
 * (`{ resources: { core, graphql, search, ... } }`). Only the three metered pools
 * are retained; each is validated. Throws if any pool is missing or malformed.
 */
export function parseBudget(value: unknown): Budget {
    const resources = (value as { resources?: unknown } | null | undefined)
        ?.resources;
    const r = resources as Record<string, unknown> | undefined;
    if (!r) throw new Error("rate_limit response missing `resources`");
    return BudgetSchema.parse({
        core: pickPool(r.core),
        graphql: pickPool(r.graphql),
        search: pickPool(r.search),
    });
}

/** Keep only the two fields our budget model needs from a rate_limit pool. */
function pickPool(pool: unknown): unknown {
    const p = pool as { limit?: unknown; remaining?: unknown } | undefined;
    if (!p) return p;
    return { limit: p.limit, remaining: p.remaining };
}
