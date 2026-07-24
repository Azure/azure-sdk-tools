#!/usr/bin/env node
/**
 * resolve-window.ts — the single deterministic resolver that turns raw workflow
 * inputs into the canonical measurement window, cohort, and `windowId`.
 *
 * Every trigger shape resolves through here: the weekly `schedule` (empty
 * window/cap inputs → rolling `today − settleDays` window, uncapped cohort),
 * `workflow_dispatch` (explicit backfill window and/or `max_prs` cap), and
 * `workflow_call`. Because identity resolution lives in one pure function, the
 * canonical `windowId` — and therefore the cross-run cache key — is knowable
 * **before** prep starts, and is byte-for-byte identical across trigger shapes
 * that describe the same `(repo, window, cohort)`.
 *
 * `prep-run.ts` consumes the resolved values instead of owning identity
 * resolution, and the workflow reads the same values (via the CLI JSON below)
 * to key `actions/cache`.
 *
 * Pure, no IO in {@link resolveWindow} — safe to import from tests. The only IO
 * is the optional CLI at the bottom, which prints the resolved record as JSON.
 */
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { RAW_SCHEMA_VERSION } from "./run-schema.ts";
import { cohortOf, computeWindowId, type Cohort } from "./window-id.ts";

const DAY_MS = 86_400_000;

/** Default settle lag (days): only PRs merged this long ago are in-window. */
export const DEFAULT_SETTLE_DAYS = 14;
/** Default rolling window length (days) when no explicit `windowStart`. */
export const DEFAULT_WINDOW_DAYS = 14;

/** UTC calendar date (YYYY-MM-DD) for an epoch-millisecond instant. */
export function isoDate(ms: number): string {
    return new Date(ms).toISOString().slice(0, 10);
}

/**
 * Raw, possibly-empty inputs as they arrive from a workflow trigger. Strings so
 * the schedule's empty-string env (`""`) and a dispatch's explicit value share
 * one code path; blanks are normalized to "absent" here, never downstream.
 */
export interface ResolveWindowInput {
    /** "owner/repo" (required). */
    repo: string;
    /** Explicit backfill window start YYYY-MM-DD; blank ⇒ rolling. */
    windowStart?: string | null;
    /** Explicit backfill window end YYYY-MM-DD; blank ⇒ settle cutoff. */
    windowEnd?: string | null;
    /** Per-window PR cap; blank/`0`/negative ⇒ uncapped full cohort. */
    maxPrs?: string | number | null;
    /** Settle lag in days; blank ⇒ {@link DEFAULT_SETTLE_DAYS}. */
    settleDays?: string | number | null;
    /** Rolling window length in days; blank ⇒ {@link DEFAULT_WINDOW_DAYS}. */
    windowDays?: string | number | null;
    /** Injectable clock (epoch ms) for deterministic rolling windows. */
    now?: number;
    /** Raw-fetch schema version; defaults to the current RAW_SCHEMA_VERSION. */
    rawSchemaVersion?: string;
}

/** The fully-resolved, identity-bearing window record. */
export interface ResolvedWindow {
    repo: string;
    windowStart: string;
    windowEnd: string;
    cohort: Cohort;
    /** Resolved PR cap; `0` = uncapped. */
    maxPrs: number;
    settleDays: number;
    windowDays: number;
    rawSchemaVersion: string;
    /** Canonical window identity (also the `ccr-cache-<windowId>-` key stem). */
    windowId: string;
}

/** Trim a string input; treat null/undefined/blank as absent. */
function blankToUndef(v: string | null | undefined): string | undefined {
    if (v === null || v === undefined) return undefined;
    const trimmed = v.trim();
    return trimmed === "" ? undefined : trimmed;
}

/** Parse an int from a string|number input, falling back when blank/invalid. */
function intOr(
    v: string | number | null | undefined,
    fallback: number,
): number {
    if (v === null || v === undefined) return fallback;
    if (typeof v === "number") return Number.isFinite(v) ? v : fallback;
    const trimmed = v.trim();
    if (trimmed === "") return fallback;
    const parsed = Number.parseInt(trimmed, 10);
    return Number.isFinite(parsed) ? parsed : fallback;
}

/**
 * Resolve raw trigger inputs into the canonical window/cohort/`windowId`.
 *
 * Explicit `windowEnd`/`windowStart` drive a historical backfill; otherwise the
 * window rolls: `end = today − settleDays` (the settle cutoff), `start = end −
 * windowDays`. Passing both bounds means the fetch is bounded by TIME, so it
 * stays safe even when uncapped. `maxPrs ≤ 0` ⇒ the uncapped cohort.
 */
export function resolveWindow(input: ResolveWindowInput): ResolvedWindow {
    const now = input.now ?? Date.now();
    const settleDays = intOr(input.settleDays, DEFAULT_SETTLE_DAYS);
    const windowDays = intOr(input.windowDays, DEFAULT_WINDOW_DAYS);
    const rawMaxPrs = intOr(input.maxPrs, 0);
    const maxPrs = Number.isFinite(rawMaxPrs) && rawMaxPrs > 0 ? rawMaxPrs : 0;
    const cohort: Cohort = cohortOf(maxPrs);
    const rawSchemaVersion = input.rawSchemaVersion ?? RAW_SCHEMA_VERSION;

    const explicitEnd = blankToUndef(input.windowEnd);
    const explicitStart = blankToUndef(input.windowStart);

    const windowEnd = explicitEnd ?? isoDate(now - settleDays * DAY_MS);
    const windowStart =
        explicitStart ?? isoDate(Date.parse(windowEnd) - windowDays * DAY_MS);

    const windowId = computeWindowId({
        repo: input.repo,
        windowStart,
        windowEnd,
        cohort,
        rawSchemaVersion,
    });

    return {
        repo: input.repo,
        windowStart,
        windowEnd,
        cohort,
        maxPrs,
        settleDays,
        windowDays,
        rawSchemaVersion,
        windowId,
    };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/resolve-window.ts --repo <owner/repo> [options]",
        "",
        "Prints the resolved window/cohort/windowId as JSON on stdout. The",
        "workflow reads this to key actions/cache before prep runs.",
        "",
        "Options:",
        "  --repo <owner/repo>      Target repository (required)",
        "  --window-start <DATE>    Backfill start YYYY-MM-DD (blank = rolling)",
        "  --window-end <DATE>      Backfill end YYYY-MM-DD (blank = settle cutoff)",
        "  --max-prs <N>            Cap PRs; 0/blank = uncapped (default: 0)",
        "  --settle-days <N>        Settle lag in days (default: 14)",
        "  --window-days <N>        Rolling window length (default: 14)",
        "  --now-ms <N>             Override clock (epoch ms) for determinism",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            repo: { type: "string" },
            "window-start": { type: "string" },
            "window-end": { type: "string" },
            "max-prs": { type: "string" },
            "settle-days": { type: "string" },
            "window-days": { type: "string" },
            "now-ms": { type: "string" },
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
    if (!v.repo) throw new Error("--repo is required");

    const nowMs = blankToUndef(v["now-ms"]);
    const resolved = resolveWindow({
        repo: v.repo,
        windowStart: v["window-start"],
        windowEnd: v["window-end"],
        maxPrs: v["max-prs"],
        settleDays: v["settle-days"],
        windowDays: v["window-days"],
        now: nowMs === undefined ? undefined : Number(nowMs),
    });
    process.stdout.write(`${JSON.stringify(resolved, null, 2)}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        process.stderr.write(
            `resolve-window: ${err instanceof Error ? err.message : String(err)}\n`,
        );
        process.exit(1);
    }
}
