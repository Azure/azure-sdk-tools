/**
 * pace.test.ts — Gate 4: the pacer planner (Track B, Phase 4).
 *
 * These pin the static-admission contract and the two properties that make
 * "resume next tick" safe instead of a livelock:
 *
 *  - `admit` fits EVERY metered pool minus the safety margin, honors
 *    `max_windows_per_tick` and `≤ 1 window/repo`, decrements as it admits, and
 *    lets a widened `remaining` (a future per-repo T1 token) admit more with NO
 *    code change.
 *  - `assertFittable` fails LOUDLY for a window that can't fit a fresh full hour
 *    (the starvation hole — a never-admitted window emits no outcome, never
 *    becomes `failed`, never retires to skipped), yet merely DEFERS a window that
 *    fits a fresh hour but not this depleted tick.
 *  - `reconcile` turns outcomes (or a missing artifact) into idempotent ledger
 *    records with a durable retry cap → `skipped.json`, never a poison loop.
 *
 * Plus two integration tests unit tests can't catch: a workflow-boundary reconcile
 * (2 successful legs + 1 failed leg) and a two-tick offline drain (tick 2 derives
 * the correct remainder: done not re-admitted, skipped honored, a failed retried,
 * previously-deferred now admitted).
 */
import { describe, expect, it } from "vitest";

import {
    admit,
    assertFittable,
    estimateCost,
    preflightShortfalls,
    readBudget,
    reconcile,
} from "../scripts/pace.ts";
import { genBacklog, type BacklogWindow } from "../scripts/gen-backlog.ts";
import { derivePending } from "../scripts/backlog.ts";
import { computeWindowId, type Cohort } from "../scripts/window-id.ts";
import { RAW_SCHEMA_VERSION } from "../scripts/run-schema.ts";
import {
    PacerConfigSchema,
    type Budget,
    type LedgerRecord,
    type OutcomeArtifact,
    type PacerConfig,
    type Skipped,
} from "../scripts/pacer-schema.ts";

// --- helpers ---------------------------------------------------------------

/** Build a config with defaults, overriding only the fields a test cares about. */
function cfg(overrides: Partial<PacerConfig> = {}): PacerConfig {
    return PacerConfigSchema.parse({
        repos: ["Azure/azure-sdk-for-go"],
        start_date: "2026-01-01",
        ...overrides,
    });
}

/** A pool budget from a single number used for both limit and remaining. */
function budget(
    core: { limit: number; remaining: number },
    graphql: { limit: number; remaining: number } = {
        limit: 5000,
        remaining: 5000,
    },
): Budget {
    return {
        core,
        graphql,
        search: { limit: 30, remaining: 30 },
    };
}

function win(
    repo: string,
    windowStart: string,
    windowEnd: string,
    cohort: Cohort = "uncapped",
): BacklogWindow {
    return {
        repo,
        windowStart,
        windowEnd,
        cohort,
        maxPrs: 0,
        windowId: computeWindowId({ repo, windowStart, windowEnd, cohort }),
    };
}

function doneOutcome(
    w: BacklogWindow,
    outcome: OutcomeArtifact["outcome"] = "produced",
): OutcomeArtifact {
    return {
        window_id: w.windowId,
        outcome,
        artifact_name: `run-${w.windowId}`,
        run_id: "100",
        attempt: 1,
        ts: "2026-04-01T00:00:00Z",
        cost: { rest: 40, graphql: 20 },
    };
}

const GO = "Azure/azure-sdk-for-go";
const PY = "Azure/azure-sdk-for-python";

// --- readBudget ------------------------------------------------------------

describe("readBudget", () => {
    it("extracts the three metered pools with limit + remaining", () => {
        const b = readBudget({
            resources: {
                core: { limit: 5000, remaining: 4321, reset: 1, used: 679 },
                graphql: { limit: 5000, remaining: 4999, reset: 1, used: 1 },
                search: { limit: 30, remaining: 30, reset: 1, used: 0 },
                // extra pools are ignored
                code_search: { limit: 10, remaining: 10 },
            },
        });
        expect(b.core).toEqual({ limit: 5000, remaining: 4321 });
        expect(b.graphql.remaining).toBe(4999);
        expect(b.search.limit).toBe(30);
    });

    it("throws on a malformed rate_limit response", () => {
        expect(() => readBudget({ nope: true })).toThrow();
        expect(() => readBudget({ resources: { core: {} } })).toThrow();
    });
});

// --- preflightShortfalls ---------------------------------------------------

describe("preflightShortfalls", () => {
    const est = { rest: 1200, graphql: 2000, search: 10, safetyMargin: 500 };

    it("returns [] when every pool covers one window plus the margin", () => {
        const b: Budget = {
            core: { limit: 5000, remaining: 1700 },
            graphql: { limit: 5000, remaining: 2500 },
            search: { limit: 30, remaining: 20 },
        };
        expect(preflightShortfalls(b, est)).toEqual([]);
    });

    it("flags a pool whose remaining is below cost + margin", () => {
        // core needs 1200+500=1700; remaining 1699 is one short.
        const b: Budget = {
            core: { limit: 5000, remaining: 1699 },
            graphql: { limit: 5000, remaining: 2500 },
            search: { limit: 30, remaining: 20 },
        };
        const out = preflightShortfalls(b, est);
        expect(out).toEqual([{ pool: "core", need: 1700, remaining: 1699 }]);
    });

    it("reports every short pool independently (core + search)", () => {
        const b: Budget = {
            core: { limit: 5000, remaining: 100 },
            graphql: { limit: 5000, remaining: 2500 },
            search: { limit: 30, remaining: 5 },
        };
        const pools = preflightShortfalls(b, est).map((s) => s.pool);
        expect(pools).toEqual(["core", "search"]);
    });
});

// --- estimateCost ----------------------------------------------------------

describe("estimateCost", () => {
    it("returns the fixed per-window constants, independent of the window", () => {
        const c = cfg({
            est_rest_calls_per_window: 900,
            est_graphql_points_per_window: 1500,
        });
        const a = estimateCost(win(GO, "2026-01-01", "2026-01-31"), c);
        const b = estimateCost(win(PY, "2026-03-01", "2026-03-31"), c);
        expect(a).toEqual({ rest: 900, graphql: 1500 });
        expect(b).toEqual(a); // same constant regardless of window
    });
});

// --- admit -----------------------------------------------------------------

describe("admit", () => {
    it("admits windows that fit every pool minus the safety margin", () => {
        const c = cfg({
            est_rest_calls_per_window: 1000,
            safety_margin: 500,
            max_windows_per_tick: 10,
        });
        const pending = [
            win(GO, "2026-01-01", "2026-01-31"),
            win(PY, "2026-01-01", "2026-01-31"),
        ];
        // core remaining 2000 − margin 500 = 1500 usable ⇒ only one 1000-cost fits.
        const r = admit(pending, budget({ limit: 5000, remaining: 2000 }), c);
        expect(r.admitted).toHaveLength(1);
        expect(r.admitted[0]?.repo).toBe(GO);
        expect(r.deferred[0]?.reason).toMatch(/insufficient core budget/);
    });

    it("honors max_windows_per_tick", () => {
        const c = cfg({
            repos: [GO, PY],
            est_rest_calls_per_window: 10,
            safety_margin: 0,
            max_windows_per_tick: 1,
        });
        const pending = [
            win(GO, "2026-01-01", "2026-01-31"),
            win(PY, "2026-01-01", "2026-01-31"),
        ];
        const r = admit(pending, budget({ limit: 5000, remaining: 5000 }), c);
        expect(r.admitted).toHaveLength(1);
        expect(r.deferred[0]?.reason).toMatch(/max_windows_per_tick/);
    });

    it("admits at most one window per repo per tick", () => {
        const c = cfg({
            est_rest_calls_per_window: 10,
            safety_margin: 0,
            max_windows_per_tick: 10,
        });
        const pending = [
            win(GO, "2026-01-01", "2026-01-31"),
            win(GO, "2026-02-01", "2026-02-28"),
            win(PY, "2026-01-01", "2026-01-31"),
        ];
        const r = admit(pending, budget({ limit: 5000, remaining: 5000 }), c);
        expect(r.admitted.map((w) => w.repo)).toEqual([GO, PY]);
        expect(r.deferred).toHaveLength(1);
        expect(r.deferred[0]?.reason).toMatch(/already admitted this tick/);
    });

    it("blocks admission when the graphql pool is too low (any pool binds)", () => {
        const c = cfg({
            est_rest_calls_per_window: 10,
            est_graphql_points_per_window: 4000,
            safety_margin: 500,
        });
        // core is fine but graphql remaining 4000 − margin 500 = 3500 < 4000.
        const r = admit(
            [win(GO, "2026-01-01", "2026-01-31")],
            budget(
                { limit: 5000, remaining: 5000 },
                { limit: 5000, remaining: 4000 },
            ),
            c,
        );
        expect(r.admitted).toHaveLength(0);
        expect(r.deferred[0]?.reason).toMatch(/insufficient graphql budget/);
    });

    it("admits more when remaining is widened (a future per-repo T1 token)", () => {
        const c = cfg({
            est_rest_calls_per_window: 1000,
            safety_margin: 500,
            max_windows_per_tick: 10,
        });
        const pending = [
            win(GO, "2026-01-01", "2026-01-31"),
            win(PY, "2026-01-01", "2026-01-31"),
        ];
        const narrow = admit(
            pending,
            budget({ limit: 5000, remaining: 2000 }),
            c,
        );
        const widened = admit(
            pending,
            budget({ limit: 15000, remaining: 15000 }),
            c,
        );
        expect(narrow.admitted).toHaveLength(1);
        expect(widened.admitted).toHaveLength(2); // no code change, just more budget
    });

    it("decrements remaining as it admits so a tick can't over-commit", () => {
        const c = cfg({
            repos: [GO, PY, "Azure/azure-sdk-for-js"],
            est_rest_calls_per_window: 1000,
            safety_margin: 0,
            max_windows_per_tick: 10,
        });
        const pending = [
            win(GO, "2026-01-01", "2026-01-31"),
            win(PY, "2026-01-01", "2026-01-31"),
            win("Azure/azure-sdk-for-js", "2026-01-01", "2026-01-31"),
        ];
        // remaining 2500: two 1000-cost admits (→500 left) then the third can't.
        const r = admit(pending, budget({ limit: 5000, remaining: 2500 }), c);
        expect(r.admitted).toHaveLength(2);
        expect(r.deferred[0]?.reason).toMatch(/insufficient core budget/);
    });
});

// --- assertFittable (starvation guard) -------------------------------------

describe("assertFittable", () => {
    it("throws when a window can't fit a fresh full hour on the core pool", () => {
        const c = cfg({
            est_rest_calls_per_window: 4800,
            safety_margin: 500,
        });
        // fresh hour usable = 5000 − 500 = 4500 < 4800.
        expect(() => {
            assertFittable(
                [win(GO, "2026-01-01", "2026-01-31")],
                budget({ limit: 5000, remaining: 5000 }),
                c,
            );
        }).toThrow(/never be admitted|defer forever/);
    });

    it("names the pool, estimate, fresh-hour budget and the fix in the error", () => {
        const c = cfg({
            est_graphql_points_per_window: 9000,
            safety_margin: 500,
        });
        let msg = "";
        try {
            assertFittable(
                [win(GO, "2026-01-01", "2026-01-31")],
                budget(
                    { limit: 5000, remaining: 5000 },
                    { limit: 5000, remaining: 5000 },
                ),
                c,
            );
        } catch (e) {
            msg = e instanceof Error ? e.message : String(e);
        }
        expect(msg).toMatch(/graphql/);
        expect(msg).toMatch(/9000/);
        expect(msg).toMatch(/4500/); // 5000 − 500
        expect(msg).toMatch(/granularity|T1|per-repo|lower est/);
    });

    it("does NOT throw for a window that fits a fresh hour but not this depleted tick", () => {
        const c = cfg({
            est_rest_calls_per_window: 1000,
            safety_margin: 500,
        });
        // limit 5000 ⇒ fresh-hour usable 4500 ≥ 1000 (fits), even though this
        // tick's remaining is only 200 (which merely defers in `admit`).
        expect(() => {
            assertFittable(
                [win(GO, "2026-01-01", "2026-01-31")],
                budget({ limit: 5000, remaining: 200 }),
                c,
            );
        }).not.toThrow();
        // ...and admit defers it rather than erroring.
        const r = admit(
            [win(GO, "2026-01-01", "2026-01-31")],
            budget({ limit: 5000, remaining: 200 }),
            c,
        );
        expect(r.admitted).toHaveLength(0);
        expect(r.deferred).toHaveLength(1);
    });
});

// --- reconcile -------------------------------------------------------------

const RECON_OPTS = { now: "2026-04-01T12:00:00Z", pacerRunId: "run-1" };

describe("reconcile", () => {
    it("writes a terminal record for a DONE outcome and never retries it", () => {
        const w = win(GO, "2026-01-01", "2026-01-31");
        const { records, newlySkipped } = reconcile(
            [w],
            [doneOutcome(w, "produced")],
            [],
            cfg(),
            RECON_OPTS,
        );
        expect(records).toHaveLength(1);
        expect(records[0]?.outcome).toBe("produced");
        expect(records[0]?.schema_version).toBe(RAW_SCHEMA_VERSION);
        expect(newlySkipped).toHaveLength(0);
    });

    it("treats thin-noop and signal-noop as done (not retried)", () => {
        const a = win(GO, "2026-01-01", "2026-01-31");
        const b = win(GO, "2026-02-01", "2026-02-28");
        const { records, newlySkipped } = reconcile(
            [a, b],
            [doneOutcome(a, "thin-noop"), doneOutcome(b, "signal-noop")],
            [],
            cfg(),
            RECON_OPTS,
        );
        expect(records.map((r) => r.outcome)).toEqual([
            "thin-noop",
            "signal-noop",
        ]);
        expect(newlySkipped).toHaveLength(0);
    });

    it("records a missing outcome artifact as failed (attempt 1)", () => {
        const w = win(GO, "2026-01-01", "2026-01-31");
        const { records, newlySkipped } = reconcile(
            [w],
            [], // leg crashed before emitting
            [],
            cfg(),
            RECON_OPTS,
        );
        expect(records[0]?.outcome).toBe("failed");
        expect(records[0]?.attempt).toBe(1);
        expect(records[0]?.run_id).toBe("run-1"); // synthesized from pacer run
        expect(newlySkipped).toHaveLength(0); // retry cap 1 ⇒ first failure retries
    });

    it("increments attempt from the prior ledger record on a repeat failure", () => {
        const w = win(GO, "2026-01-01", "2026-01-31");
        const prior: LedgerRecord = {
            window_id: w.windowId,
            repo: GO,
            window_start: w.windowStart,
            window_end: w.windowEnd,
            cohort: "uncapped",
            schema_version: RAW_SCHEMA_VERSION,
            outcome: "failed",
            attempt: 1,
            run_id: "run-0",
            artifact_name: `run-${w.windowId}`,
            ts: "2026-03-01T00:00:00Z",
        };
        const { records, newlySkipped } = reconcile(
            [w],
            [doneOutcome(w, "failed")],
            [prior],
            cfg({ retry_cap: 1 }),
            RECON_OPTS,
        );
        expect(records[0]?.attempt).toBe(2);
        // second failure exceeds retry_cap 1 ⇒ retired to skipped.json.
        expect(newlySkipped).toHaveLength(1);
        expect(newlySkipped[0]?.window_id).toBe(w.windowId);
        expect(newlySkipped[0]?.reason).toMatch(/retry cap/);
    });

    it("does not skip on the first failure with the default retry cap", () => {
        const w = win(GO, "2026-01-01", "2026-01-31");
        const { newlySkipped } = reconcile(
            [w],
            [doneOutcome(w, "failed")],
            [],
            cfg(),
            RECON_OPTS,
        );
        expect(newlySkipped).toHaveLength(0);
    });
});

// --- workflow-boundary reconcile (2 success legs + 1 failed leg) -----------

describe("workflow-boundary reconcile", () => {
    it("records all three admitted windows despite one failed leg", () => {
        const a = win(GO, "2026-01-01", "2026-01-31");
        const b = win(PY, "2026-01-01", "2026-01-31");
        const cfail = win("Azure/azure-sdk-for-js", "2026-01-01", "2026-01-31");
        // The failed leg uploaded no outcome artifact (if: always ledger still runs).
        const { records, newlySkipped } = reconcile(
            [a, b, cfail],
            [doneOutcome(a, "produced"), doneOutcome(b, "thin-noop")],
            [],
            cfg(),
            RECON_OPTS,
        );
        const byId = new Map(records.map((r) => [r.window_id, r]));
        expect(byId.get(a.windowId)?.outcome).toBe("produced");
        expect(byId.get(b.windowId)?.outcome).toBe("thin-noop");
        expect(byId.get(cfail.windowId)?.outcome).toBe("failed");
        expect(records).toHaveLength(3);
        expect(newlySkipped).toHaveLength(0); // first failure retries, not skipped
    });
});

// --- two-tick offline drain ------------------------------------------------

describe("two-tick offline drain", () => {
    it("derives the correct remainder on tick 2 (done not re-admitted, failed retried, deferred now admitted)", () => {
        const config = cfg({
            repos: [GO, PY],
            est_rest_calls_per_window: 1000,
            safety_margin: 500,
            max_windows_per_tick: 2,
            retry_cap: 1,
        });
        // now = 2026-04-01 ⇒ Jan + Feb settled for both repos (4 windows).
        const now = Date.parse("2026-04-01T00:00:00Z");
        const desired = genBacklog(config, { now });
        expect(desired).toHaveLength(4);

        // --- tick 1: nothing done yet ---
        const t1Pending = derivePending(desired, [], { windows: [] }).pending;
        const t1 = admit(
            t1Pending,
            budget({ limit: 15000, remaining: 15000 }),
            config,
        );
        // ≤ 1/repo + cap 2 ⇒ admit Jan for GO and PY; Feb for both deferred.
        expect(t1.admitted).toHaveLength(2);
        expect(t1.admitted.map((w) => w.repo).sort()).toEqual([GO, PY]);

        // GO Jan produced; PY Jan failed (missing outcome).
        const goJan = t1.admitted.find((w) => w.repo === GO)!;
        const pyJan = t1.admitted.find((w) => w.repo === PY)!;
        const r1 = reconcile(
            t1.admitted,
            [doneOutcome(goJan, "produced")],
            [],
            config,
            RECON_OPTS,
        );
        const ledger = r1.records;
        const skipped: Skipped = { windows: r1.newlySkipped };

        // --- tick 2: derive from the updated ledger ---
        const t2Pending = derivePending(desired, ledger, skipped).pending;
        const t2Ids = t2Pending.map((w) => w.windowId);
        // GO Jan is DONE ⇒ not pending. PY Jan failed ⇒ still pending (retry).
        // Both Feb windows were deferred ⇒ still pending.
        expect(t2Ids).toContain(pyJan.windowId);
        expect(t2Ids).not.toContain(goJan.windowId);
        expect(t2Pending).toHaveLength(3);

        const t2 = admit(
            t2Pending,
            budget({ limit: 15000, remaining: 15000 }),
            config,
        );
        // cap 2 + ≤1/repo: admit PY Jan (retry) + GO Feb; PY Feb deferred.
        expect(t2.admitted).toHaveLength(2);
        expect(t2.admitted.map((w) => w.repo).sort()).toEqual([GO, PY]);
        expect(t2.admitted.some((w) => w.windowId === pyJan.windowId)).toBe(
            true,
        );

        // PY Jan fails AGAIN ⇒ attempt 2 > retry_cap 1 ⇒ retired to skipped.
        const r2 = reconcile(
            t2.admitted,
            t2.admitted
                .filter((w) => w.repo === GO)
                .map((w) => doneOutcome(w, "produced")),
            ledger,
            config,
            RECON_OPTS,
        );
        expect(r2.newlySkipped.map((s) => s.window_id)).toContain(
            pyJan.windowId,
        );

        // --- tick 3: PY Jan is now skipped ⇒ never pending again ---
        const ledger3 = [...ledger, ...r2.records];
        const skipped3: Skipped = {
            windows: [...skipped.windows, ...r2.newlySkipped],
        };
        const t3Pending = derivePending(desired, ledger3, skipped3).pending;
        expect(t3Pending.map((w) => w.windowId)).not.toContain(pyJan.windowId);
    });
});
