#!/usr/bin/env node
/**
 * pace.ts — the pacer's planner (Track B, Phase 4).
 *
 * The pacer is a thin hourly cron over this tested TypeScript. Its whole job is a
 * **static conservative budget check + resume-next-tick** — deliberately NOT a
 * predictive estimator (see deferred-and-scope.md, D15). Each tick:
 *
 *   plan   = reconcile(previous tick) → derivePending(gen-backlog − ledger −
 *            skipped) → assertFittable (starvation guard) → admit (fits every
 *            metered pool minus margin, ≤ 1 window/repo, ≤ max_windows_per_tick).
 *
 * Every function here is pure and unit-tested; the CLI at the bottom is the only
 * IO (reads `gh api rate_limit`, the state-branch `ledger/`+`skipped.json`, and
 * the downloaded outcome artifacts; prints the admitted matrix / writes the
 * updated ledger).
 *
 * Two properties make the "resume next tick" safe rather than a livelock:
 *
 *  1. **`assertFittable`** — a window whose fixed estimate can't fit a _fresh full
 *     hour_ on some pool could never be admitted and would defer forever,
 *     invisibly (a never-admitted window emits no outcome, never becomes
 *     `failed`, never hits the retry cap). Because the estimate is a constant,
 *     that's a whole-config error, so we fail the plan LOUDLY at admission time
 *     instead of letting it hide as a perpetually-pending window.
 *  2. **retry cap** — a `failed` (or missing-outcome) window is retried a bounded
 *     number of times (durable `attempt` in the ledger) then retired to
 *     `skipped.json`, so a poison window can't wedge the backlog.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { genBacklog, type BacklogWindow } from "./gen-backlog.ts";
import { derivePending, readLedgerDir, readSkippedFile } from "./backlog.ts";
import { RAW_SCHEMA_VERSION } from "./run-schema.ts";
import { runArtifactBase } from "./window-id.ts";
import {
    parseBudget,
    parseOutcomeArtifact,
    parsePacerConfig,
    type Budget,
    type LedgerRecord,
    type OutcomeArtifact,
    type PacerConfig,
    type PoolBudget,
    type Skipped,
    type SkippedWindow,
} from "./pacer-schema.ts";
import { ghApiJsonAsync } from "./utils.ts";

// ---------------------------------------------------------------------------
// Budget.
// ---------------------------------------------------------------------------

/**
 * `readBudget` is just {@link parseBudget} (the parse lives in the schema module
 * so it can be reused). Exposed here as the pacer's named entry point; returns the
 * metered pools with BOTH `limit` (fresh-hour ceiling) and `remaining`.
 */
export function readBudget(rateLimitResponse: unknown): Budget {
    return parseBudget(rateLimitResponse);
}

/** A window's static per-pool cost estimate (a fixed constant, not measured). */
export interface CostEstimate {
    /** Core-REST calls (the binding pool). */
    rest: number;
    /** GraphQL points (hygiene meter). */
    graphql: number;
}

/**
 * The static conservative cost of running one window. It is a **fixed constant**
 * from config (`est_*_per_window`), independent of the window's actual PR count —
 * no count probe, no per-connection cardinality model. Same for every window at a
 * given granularity, which is exactly what makes {@link assertFittable} a global
 * property of the config.
 */
export function estimateCost(
    _window: BacklogWindow,
    config: PacerConfig,
): CostEstimate {
    return {
        rest: config.est_rest_calls_per_window,
        graphql: config.est_graphql_points_per_window,
    };
}

/** The two metered pools a cost estimate maps onto: rest→core, graphql→graphql. */
interface MeteredPool {
    name: "core" | "graphql";
    /** The window's estimated cost on this pool. */
    cost: (e: CostEstimate) => number;
    /** The live pool from the budget. */
    pool: (b: Budget) => PoolBudget;
}

const METERED_POOLS: MeteredPool[] = [
    { name: "core", cost: (e) => e.rest, pool: (b) => b.core },
    { name: "graphql", cost: (e) => e.graphql, pool: (b) => b.graphql },
];

/**
 * **Starvation guard.** Assert every pending window's fixed estimate fits a FRESH
 * FULL HOUR's usable budget (`pool.limit − safety_margin`) on every metered pool.
 *
 * If it doesn't, that window — and, since the estimate is a constant, every window
 * at this granularity — can never be admitted and would defer forever without ever
 * surfacing (no outcome ⇒ never `failed` ⇒ never retired to `skipped.json`). So
 * this throws LOUDLY (the CLI exits non-zero) with the offending pool, the
 * estimate, the fresh-hour budget, and the fix — the whole-config error is caught
 * at admission time, never left to hide as a perpetually-pending window.
 *
 * This uses `limit` (the ceiling), NOT this tick's `remaining`: a window that fits
 * a fresh hour but not this depleted tick is fine — it's merely deferred.
 */
export function assertFittable(
    pending: BacklogWindow[],
    budget: Budget,
    config: PacerConfig,
): void {
    for (const window of pending) {
        const est = estimateCost(window, config);
        for (const m of METERED_POOLS) {
            const freshHourUsable = m.pool(budget).limit - config.safety_margin;
            const cost = m.cost(est);
            if (cost > freshHourUsable) {
                throw new Error(
                    `unfittable window ${window.windowId}: estimated ${String(
                        cost,
                    )} on the ${m.name} pool exceeds a fresh hour's usable budget ` +
                        `(${String(m.pool(budget).limit)} limit − ${String(
                            config.safety_margin,
                        )} margin = ${String(freshHourUsable)}). This window can ` +
                        `never be admitted and would defer forever. Fix: lower ` +
                        `est_${m.name === "core" ? "rest_calls" : "graphql_points"}_per_window, ` +
                        `raise the ${m.name} ceiling (per-repo token, T1), or configure a finer granularity.`,
                );
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Preflight budget guard (C2).
// ---------------------------------------------------------------------------

/** Static per-window cost + headroom used by the child-loop preflight guard. */
export interface PreflightEstimate {
    /** Core-REST calls one window costs (the binding pool). */
    rest: number;
    /** GraphQL points one window costs (hygiene meter). */
    graphql: number;
    /** `search` calls one window costs (the 30/min pool). */
    search: number;
    /** Headroom kept free on the large pools (core/graphql); not applied to search. */
    safetyMargin: number;
}

/** A pool whose live `remaining` cannot cover one window plus the margin. */
export interface PoolShortfall {
    pool: "core" | "graphql" | "search";
    /** What the window needs right now: `cost + safetyMargin`. */
    need: number;
    /** The pool's live `remaining`. */
    remaining: number;
}

/**
 * **Preflight budget guard.** Unlike {@link assertFittable} (a fresh-hour
 * feasibility check over the whole config), this checks THIS MOMENT's live
 * `remaining` on every pool against a single window's static cost plus the safety
 * margin. It answers "can I safely run one window right now?" — the check the
 * child loop runs before it spends a single call, so it aborts cleanly instead of
 * exhausting a shared pool mid-fetch. Returns one entry per short pool (empty ⇒
 * clear to proceed).
 */
export function preflightShortfalls(
    budget: Budget,
    est: PreflightEstimate,
): PoolShortfall[] {
    // The safety margin is a headroom concept sized for the large hourly pools
    // (core/graphql, thousands of units). The `search` pool is tiny (30/min), so
    // applying the same absolute margin there would guarantee a false shortfall;
    // it is guarded against its own per-window cost only (its fast refill is the
    // real backstop).
    const checks: {
        pool: PoolShortfall["pool"];
        cost: number;
        remaining: number;
        margin: number;
    }[] = [
        {
            pool: "core",
            cost: est.rest,
            remaining: budget.core.remaining,
            margin: est.safetyMargin,
        },
        {
            pool: "graphql",
            cost: est.graphql,
            remaining: budget.graphql.remaining,
            margin: est.safetyMargin,
        },
        {
            pool: "search",
            cost: est.search,
            remaining: budget.search.remaining,
            margin: 0,
        },
    ];
    const out: PoolShortfall[] = [];
    for (const c of checks) {
        const need = c.cost + c.margin;
        if (c.remaining < need) {
            out.push({ pool: c.pool, need, remaining: c.remaining });
        }
    }
    return out;
}

// ---------------------------------------------------------------------------
// Admission.
// ---------------------------------------------------------------------------

/** The outcome of an admission pass. */
export interface AdmitResult {
    /** Windows to dispatch this tick. */
    admitted: BacklogWindow[];
    /** Windows held for a later tick (budget/cap/per-repo), with the reason. */
    deferred: { window: BacklogWindow; reason: string }[];
}

/**
 * Greedily admit pending windows that fit `remaining − safety_margin` on EVERY
 * metered pool, honoring two hard limits:
 *
 *  - **`max_windows_per_tick`** — the real bound on aggregate cross-repo GraphQL
 *    exposure; whatever doesn't fit is picked up next tick (free, cached).
 *  - **≤ 1 window / repo / tick** — combined with the loop's per-repo child
 *    concurrency group, this serializes same-repo work while still allowing
 *    multiple repos to run concurrent lanes within budget.
 *
 * `remaining` is decremented as windows are admitted so a tick can't over-commit a
 * pool. Assumes {@link assertFittable} already ran, so any window that doesn't fit
 * THIS tick genuinely fits a fresh hour and is only deferred (never starved).
 */
export function admit(
    pending: BacklogWindow[],
    budget: Budget,
    config: PacerConfig,
): AdmitResult {
    const admitted: BacklogWindow[] = [];
    const deferred: { window: BacklogWindow; reason: string }[] = [];
    const reposUsed = new Set<string>();
    // Mutable copy of each pool's remaining, decremented as we admit.
    const remaining: Record<"core" | "graphql", number> = {
        core: budget.core.remaining,
        graphql: budget.graphql.remaining,
    };

    for (const window of pending) {
        if (admitted.length >= config.max_windows_per_tick) {
            deferred.push({ window, reason: "max_windows_per_tick reached" });
            continue;
        }
        if (reposUsed.has(window.repo)) {
            deferred.push({
                window,
                reason: `repo ${window.repo} already admitted this tick`,
            });
            continue;
        }
        const est = estimateCost(window, config);
        const blocking = METERED_POOLS.find(
            (m) => m.cost(est) > remaining[m.name] - config.safety_margin,
        );
        if (blocking) {
            deferred.push({
                window,
                reason: `insufficient ${blocking.name} budget this tick`,
            });
            continue;
        }
        admitted.push(window);
        reposUsed.add(window.repo);
        remaining.core -= est.rest;
        remaining.graphql -= est.graphql;
    }

    return { admitted, deferred };
}

// ---------------------------------------------------------------------------
// Reconciliation.
// ---------------------------------------------------------------------------

/** The updated durable state a reconcile pass produces. */
export interface ReconcileResult {
    /** New/updated ledger records (one per admitted window), keyed by window_id. */
    records: LedgerRecord[];
    /** Windows newly retired to skipped.json (retry cap exceeded). */
    newlySkipped: SkippedWindow[];
}

export interface ReconcileOptions {
    /** ISO timestamp for records written when an outcome artifact is missing. */
    now: string;
    /** The GitHub run id of the pacer tick (used for synthesized failed records). */
    pacerRunId: string;
}

/**
 * Reconcile a completed tick: every admitted window MUST have an outcome artifact;
 * a missing one means the backfill leg never emitted (crash/cancel) and is treated
 * as `failed`. For each admitted window:
 *
 *  - **DONE outcome** (`produced|thin-noop|signal-noop`) → a terminal ledger
 *    record; never retried.
 *  - **`failed` / missing** → a `failed` ledger record with `attempt` incremented
 *    from any prior record. Once `attempt` exceeds `retry_cap` (default 1 ⇒ the
 *    second failure), the window is ALSO retired to `skipped.json` so it can't
 *    poison-loop; derivePending drops skipped windows regardless of the record.
 *
 * `attempt` is the DURABLE per-window attempt counter carried in the ledger, not
 * the GitHub run attempt — so the retry cap survives across ticks and re-dispatch.
 */
export function reconcile(
    admitted: BacklogWindow[],
    outcomes: OutcomeArtifact[],
    priorLedger: LedgerRecord[],
    config: PacerConfig,
    opts: ReconcileOptions,
): ReconcileResult {
    const outcomeById = new Map(outcomes.map((o) => [o.window_id, o]));
    const priorById = new Map(priorLedger.map((r) => [r.window_id, r]));
    const records: LedgerRecord[] = [];
    const newlySkipped: SkippedWindow[] = [];

    for (const window of admitted) {
        const prior = priorById.get(window.windowId);
        const priorAttempt = prior?.attempt ?? 0;
        const outcome = outcomeById.get(window.windowId);

        if (outcome && outcome.outcome !== "failed") {
            // Terminal success (DONE). Attempt count is whatever produced it.
            records.push({
                window_id: window.windowId,
                repo: window.repo,
                window_start: window.windowStart,
                window_end: window.windowEnd,
                cohort: window.cohort,
                schema_version: RAW_SCHEMA_VERSION,
                outcome: outcome.outcome,
                attempt: Math.max(priorAttempt, outcome.attempt),
                run_id: outcome.run_id,
                artifact_name: outcome.artifact_name,
                ts: outcome.ts,
            });
            continue;
        }

        // Failure path: explicit `failed` outcome OR no artifact at all.
        const attempt = priorAttempt + 1;
        records.push({
            window_id: window.windowId,
            repo: window.repo,
            window_start: window.windowStart,
            window_end: window.windowEnd,
            cohort: window.cohort,
            schema_version: RAW_SCHEMA_VERSION,
            outcome: "failed",
            attempt,
            run_id: outcome?.run_id ?? opts.pacerRunId,
            artifact_name:
                outcome?.artifact_name ?? runArtifactBase(window.windowId),
            ts: outcome?.ts ?? opts.now,
        });
        if (attempt > config.retry_cap) {
            newlySkipped.push({
                window_id: window.windowId,
                reason: `exceeded retry cap (${String(config.retry_cap)}) after ${String(
                    attempt,
                )} failed attempts`,
                ts: opts.now,
            });
        }
    }

    return { records, newlySkipped };
}

// ---------------------------------------------------------------------------
// IO wiring (the CLI). Everything above is pure + unit-tested.
// ---------------------------------------------------------------------------

/** Fetch + parse the live budget from `gh api rate_limit`. */
async function fetchBudget(): Promise<Budget> {
    // `--paginate --slurp` wraps the single-page response in an array.
    const res = await ghApiJsonAsync<unknown>("rate_limit");
    const body: unknown = Array.isArray(res) ? (res as unknown[])[0] : res;
    return readBudget(body);
}

/** Load config + derive the settled pending backlog from a state checkout. */
function loadPending(
    config: PacerConfig,
    stateDir: string,
    now: number,
    settleDays: number | undefined,
): {
    pending: BacklogWindow[];
    ledger: LedgerRecord[];
    skipped: Skipped;
} {
    const desired = genBacklog(config, { now, settleDays });
    const ledger = stateDir ? readLedgerDir(path.join(stateDir, "ledger")) : [];
    const skipped = stateDir
        ? readSkippedFile(path.join(stateDir, "skipped.json"))
        : { windows: [] };
    const { pending } = derivePending(desired, ledger, skipped);
    return { pending, ledger, skipped };
}

/** Shape emitted for the backfill matrix (one entry per admitted window). */
interface MatrixEntry {
    window_id: string;
    repo: string;
    window_start: string;
    window_end: string;
    max_prs: string;
}

function toMatrixEntry(w: BacklogWindow): MatrixEntry {
    return {
        window_id: w.windowId,
        repo: w.repo,
        window_start: w.windowStart,
        window_end: w.windowEnd,
        max_prs: w.maxPrs > 0 ? String(w.maxPrs) : "",
    };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/pace.ts plan --config pacer/config.json --state <dir> [options]",
        "  node scripts/pace.ts reconcile --config pacer/config.json --state <dir> \\",
        "      --admitted <file> --outcomes <dir> [--out-state <dir>]",
        "",
        "Modes:",
        "  plan        Derive pending, assert fittable, admit; print the matrix.",
        "  reconcile   Fold this tick's outcome artifacts into ledger/ + skipped.json.",
        "",
        "Options:",
        "  --config <path>       pacer/config.json (required)",
        "  --state <dir>         ccr-pacer-state checkout (ledger/ + skipped.json)",
        "  --settle-days <N>     Settle lag in days (default: 14)",
        "  --now-ms <N>          Override clock (epoch ms) for determinism",
        "  --dry-run             (plan) Print the plan + pending count; touch nothing",
        "  --budget-file <path>  (plan) Read rate_limit JSON from a file (else `gh`)",
        "  --admitted <path>     (reconcile) JSON array of admitted matrix entries",
        "  --outcomes <dir>      (reconcile) Dir of downloaded outcome-*.json",
        "  --out-state <dir>     (reconcile) Write ledger/ + skipped.json here (else --state)",
        "  --pacer-run-id <id>   (reconcile) This pacer run id (default: local)",
        "  -h, --help",
    ].join("\n");
}

interface CliValues {
    config?: string;
    state?: string;
    "settle-days"?: string;
    "now-ms"?: string;
    "dry-run"?: boolean;
    "budget-file"?: string;
    admitted?: string;
    outcomes?: string;
    "out-state"?: string;
    "pacer-run-id"?: string;
    help?: boolean;
}

async function runPlan(v: CliValues): Promise<void> {
    if (!v.config) throw new Error("--config is required");
    const config = parsePacerConfig(
        JSON.parse(fs.readFileSync(v.config, "utf8")),
    );
    const settleDays =
        v["settle-days"] === undefined
            ? undefined
            : Number.parseInt(v["settle-days"], 10);
    const now = v["now-ms"] === undefined ? Date.now() : Number(v["now-ms"]);
    const stateDir = v.state ?? "";

    const { pending } = loadPending(config, stateDir, now, settleDays);

    const budget = v["budget-file"]
        ? readBudget(JSON.parse(fs.readFileSync(v["budget-file"], "utf8")))
        : await fetchBudget();

    // Starvation guard BEFORE admission — a whole-config error fails loudly here.
    assertFittable(pending, budget, config);

    const { admitted, deferred } = admit(pending, budget, config);
    const matrix = admitted.map(toMatrixEntry);

    if (v["dry-run"]) {
        process.stdout.write(
            `${JSON.stringify(
                {
                    pending: pending.length,
                    admitted: matrix,
                    deferred: deferred.map((d) => ({
                        window_id: d.window.windowId,
                        reason: d.reason,
                    })),
                    estimate_per_window: {
                        rest: config.est_rest_calls_per_window,
                        graphql: config.est_graphql_points_per_window,
                    },
                    budget,
                },
                null,
                2,
            )}\n`,
        );
        return;
    }

    // Non-dry-run `plan`: emit just the matrix (for GITHUB_OUTPUT `admitted`).
    process.stdout.write(`${JSON.stringify(matrix)}\n`);
}

function runReconcile(v: CliValues): void {
    if (!v.config) throw new Error("--config is required");
    if (!v.admitted) throw new Error("--admitted is required");
    if (!v.outcomes) throw new Error("--outcomes is required");
    const config = parsePacerConfig(
        JSON.parse(fs.readFileSync(v.config, "utf8")),
    );
    const stateDir = v.state ?? "";
    const outStateDir = v["out-state"] ?? stateDir;
    if (!outStateDir) throw new Error("--out-state or --state is required");

    const admittedRaw: unknown = JSON.parse(
        fs.readFileSync(v.admitted, "utf8"),
    );
    const admittedEntries = admittedRaw as MatrixEntry[];
    const admitted: BacklogWindow[] = admittedEntries.map((e) => ({
        repo: e.repo,
        windowStart: e.window_start,
        windowEnd: e.window_end,
        cohort: cohortFromMaxPrs(e.max_prs),
        maxPrs: e.max_prs ? Number(e.max_prs) : 0,
        windowId: e.window_id,
    }));

    const outcomes = readOutcomeDir(v.outcomes);
    const priorLedger = stateDir
        ? readLedgerDir(path.join(stateDir, "ledger"))
        : [];
    const priorSkipped = stateDir
        ? readSkippedFile(path.join(stateDir, "skipped.json"))
        : { windows: [] };

    const { records, newlySkipped } = reconcile(
        admitted,
        outcomes,
        priorLedger,
        config,
        {
            now: new Date().toISOString(),
            pacerRunId: v["pacer-run-id"] ?? "local",
        },
    );

    writeLedger(outStateDir, records);
    if (newlySkipped.length > 0) {
        writeSkipped(
            path.join(outStateDir, "skipped.json"),
            priorSkipped,
            newlySkipped,
        );
    }
    process.stderr.write(
        `reconciled ${String(records.length)} record(s); ${String(
            newlySkipped.length,
        )} newly skipped\n`,
    );
}

/** Cohort token from a matrix `max_prs` string (empty ⇒ uncapped). */
function cohortFromMaxPrs(maxPrs: string): BacklogWindow["cohort"] {
    const n = maxPrs ? Number(maxPrs) : 0;
    return n > 0
        ? (`cap-${String(Math.trunc(n))}` as BacklogWindow["cohort"])
        : "uncapped";
}

/** Read + validate every downloaded outcome-*.json in a directory. */
function readOutcomeDir(dir: string): OutcomeArtifact[] {
    if (!fs.existsSync(dir)) return [];
    const out: OutcomeArtifact[] = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        // actions/download-artifact may nest each artifact in its own subdir.
        if (entry.isDirectory()) {
            out.push(...readOutcomeDir(path.join(dir, entry.name)));
            continue;
        }
        if (!entry.name.startsWith("outcome-") || !entry.name.endsWith(".json"))
            continue;
        const raw: unknown = JSON.parse(
            fs.readFileSync(path.join(dir, entry.name), "utf8"),
        );
        out.push(parseOutcomeArtifact(raw));
    }
    return out;
}

/** Write one `ledger/<window_id>.json` per record (idempotent overwrite). */
function writeLedger(stateDir: string, records: LedgerRecord[]): void {
    const dir = path.join(stateDir, "ledger");
    fs.mkdirSync(dir, { recursive: true });
    for (const r of records) {
        fs.writeFileSync(
            path.join(dir, `${r.window_id}.json`),
            `${JSON.stringify(r, null, 2)}\n`,
        );
    }
}

/** Merge new skipped windows into skipped.json (dedup by window_id). */
function writeSkipped(
    file: string,
    prior: Skipped,
    additions: SkippedWindow[],
): void {
    const byId = new Map(prior.windows.map((w) => [w.window_id, w]));
    for (const w of additions) byId.set(w.window_id, w);
    const merged: Skipped = { windows: [...byId.values()] };
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, `${JSON.stringify(merged, null, 2)}\n`);
}

async function main(): Promise<void> {
    const [mode, ...rest] = process.argv.slice(2);
    const parsed = nodeParseArgs({
        args: rest,
        options: {
            config: { type: "string" },
            state: { type: "string" },
            "settle-days": { type: "string" },
            "now-ms": { type: "string" },
            "dry-run": { type: "boolean", default: false },
            "budget-file": { type: "string" },
            admitted: { type: "string" },
            outcomes: { type: "string" },
            "out-state": { type: "string" },
            "pacer-run-id": { type: "string" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    const v = parsed.values as CliValues;
    if (v.help || !mode || mode === "-h" || mode === "--help") {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    if (mode === "plan") {
        await runPlan(v);
        return;
    }
    if (mode === "reconcile") {
        runReconcile(v);
        return;
    }
    throw new Error(`unknown mode "${mode}" (expected plan|reconcile)`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((err: unknown) => {
        process.stderr.write(
            `pace: ${err instanceof Error ? err.message : String(err)}\n`,
        );
        process.exit(1);
    });
}
