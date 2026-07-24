#!/usr/bin/env node
/**
 * emit-outcome.ts — classify a loop run's terminal outcome and write the typed
 * `outcome-<window_id>.json` the pacer consumes (Track B, Phase 3).
 *
 * The outcome is the loop's completion contract. It is deterministic and derived
 * only from observable signals, never guessed:
 *
 *   failed       — the run errored (non-zero exit) OR the run-JSON artifact
 *                  upload / safe-output processing failed. `produced` is claimed
 *                  ONLY after the upload is confirmed; anything short of that with
 *                  real work to show is `failed` (eligible for one retry).
 *   thin-noop    — fewer than `minPrs` settled PRs: not enough signal to measure.
 *                  A legitimate DONE outcome, not a failure.
 *   signal-noop  — ≥ `minPrs` PRs but the agent explicitly called `noop` (nothing
 *                  worth reporting this window). Also a DONE outcome.
 *   produced     — ≥ `minPrs` PRs, not a noop, run-JSON upload confirmed.
 *
 * `artifact_name` is derived from `window_id` (`run-<window_id>`) — never read
 * from an unstable upload temp id — so the pacer correlates the outcome to the
 * run artifact offline. `cost` is the OBSERVED budget spend, logged for
 * `--dry-run` visibility; it is never fed into an estimator.
 *
 * {@link classifyOutcome} and {@link buildOutcome} are pure (no IO) and unit
 * tested against every scenario; `main` wires file IO around them.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import {
    parseOutcomeArtifact,
    type Cost,
    type Outcome,
    type OutcomeArtifact,
} from "./pacer-schema.ts";
import { runArtifactBase } from "./window-id.ts";

/** The observable signals a run produces, in classification-precedence order. */
export interface OutcomeSignals {
    /** Settled PR count prep selected for this window. */
    prCount: number;
    /** Minimum PRs needed for a measurable window (config.minPrs). */
    minPrs: number;
    /** The agent explicitly reported nothing worth acting on. */
    agentNoop: boolean;
    /** The run-JSON artifact upload (+ safe-output processing) was confirmed. */
    uploadConfirmed: boolean;
    /**
     * A hard failure occurred before/independent of classification (non-zero
     * exit, prep crash, safe-output processing error). Overrides everything.
     */
    hardFailure?: boolean;
}

/**
 * Map observable signals to the single terminal outcome. Precedence:
 * hardFailure → thin-noop → signal-noop → produced/failed(upload). A hard
 * failure wins over a low PR count so a crashed run with `prCount === 0` is
 * `failed` (retryable), not mistaken for a legitimate `thin-noop`.
 */
export function classifyOutcome(s: OutcomeSignals): Outcome {
    if (s.hardFailure) return "failed";
    if (s.prCount < s.minPrs) return "thin-noop";
    if (s.agentNoop) return "signal-noop";
    // Real work to report: only `produced` once the upload is confirmed.
    return s.uploadConfirmed ? "produced" : "failed";
}

export interface BuildOutcomeInput {
    windowId: string;
    outcome: Outcome;
    /** GitHub Actions run id (string form). */
    runId: string;
    /** GitHub Actions run attempt (1-based). */
    attempt: number;
    /** ISO-8601 emit timestamp (injectable for deterministic tests). */
    ts: string;
    cost: Cost;
}

/**
 * Assemble + validate the outcome artifact. `artifact_name` is derived
 * deterministically from `windowId`, so it is stable across attempts and
 * knowable without inspecting the upload.
 */
export function buildOutcome(input: BuildOutcomeInput): OutcomeArtifact {
    const artifact: OutcomeArtifact = {
        window_id: input.windowId,
        outcome: input.outcome,
        artifact_name: runArtifactBase(input.windowId),
        run_id: input.runId,
        attempt: input.attempt,
        ts: input.ts,
        cost: input.cost,
    };
    // Validate on write — the schema is the pacer's contract.
    return parseOutcomeArtifact(artifact);
}

/** The outcome-artifact filename for a window id. */
export function outcomeFileName(windowId: string): string {
    return `outcome-${windowId}.json`;
}

// ---------------------------------------------------------------------------
// IO wiring.
// ---------------------------------------------------------------------------

function intArg(v: string | undefined, name: string): number {
    if (v === undefined) throw new Error(`--${name} is required`);
    const n = Number.parseInt(v, 10);
    if (!Number.isFinite(n)) throw new Error(`--${name} must be an integer`);
    return n;
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/emit-outcome.ts --window-id <id> --pr-count <N> \\",
        "    --min-prs <N> --run-id <id> --attempt <N> --out-dir <dir> [signals]",
        "",
        "Classifies the run and writes outcome-<window_id>.json.",
        "",
        "Options:",
        "  --window-id <id>       Canonical window id (required)",
        "  --pr-count <N>         Settled PR count prep selected (required)",
        "  --min-prs <N>          Minimum PRs for a measurable window (required)",
        "  --run-id <id>          GitHub run id (required)",
        "  --attempt <N>          GitHub run attempt (required)",
        "  --agent-noop           Agent explicitly called noop",
        "  --upload-confirmed     Run-JSON artifact upload confirmed",
        "  --hard-failure         A hard failure occurred (overrides all)",
        "  --cost-rest <N>        Observed core REST calls (default 0)",
        "  --cost-graphql <N>     Observed GraphQL points (default 0)",
        "  --out-dir <dir>        Write outcome-<id>.json here (else stdout)",
        "  --ts <iso>             Override emit timestamp (determinism)",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            "window-id": { type: "string" },
            "pr-count": { type: "string" },
            "min-prs": { type: "string" },
            "run-id": { type: "string" },
            attempt: { type: "string" },
            "agent-noop": { type: "boolean", default: false },
            "upload-confirmed": { type: "boolean", default: false },
            "hard-failure": { type: "boolean", default: false },
            "cost-rest": { type: "string" },
            "cost-graphql": { type: "string" },
            "out-dir": { type: "string" },
            ts: { type: "string" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    const v = parsed.values;
    const windowId = v["window-id"];
    if (!windowId) throw new Error("--window-id is required");
    const runId = v["run-id"];
    if (!runId) throw new Error("--run-id is required");

    const outcome = classifyOutcome({
        prCount: intArg(v["pr-count"], "pr-count"),
        minPrs: intArg(v["min-prs"], "min-prs"),
        agentNoop: v["agent-noop"],
        uploadConfirmed: v["upload-confirmed"],
        hardFailure: v["hard-failure"],
    });

    const artifact = buildOutcome({
        windowId,
        outcome,
        runId,
        attempt: intArg(v.attempt, "attempt"),
        ts: v.ts ?? new Date().toISOString(),
        cost: {
            rest: v["cost-rest"] ? Number(v["cost-rest"]) : 0,
            graphql: v["cost-graphql"] ? Number(v["cost-graphql"]) : 0,
        },
    });

    const text = JSON.stringify(artifact, null, 2);
    if (v["out-dir"]) {
        fs.mkdirSync(v["out-dir"], { recursive: true });
        const out = `${v["out-dir"]}/${outcomeFileName(windowId)}`;
        fs.writeFileSync(out, text);
        process.stderr.write(`wrote ${out} (${artifact.outcome})\n`);
    } else {
        process.stdout.write(text + "\n");
    }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        process.stderr.write(
            `emit-outcome: ${err instanceof Error ? err.message : String(err)}\n`,
        );
        process.exit(1);
    }
}
