#!/usr/bin/env node
/**
 * gen-backlog.ts — the pure, settled-only window expander (Track B, Phase 3).
 *
 * From `pacer/config.json` (`{ repos, start_date, granularity, max_prs? }`) and a
 * "today", expand `start_date … (today − settleDays)` into the full set of
 * candidate `(repo, window)` pairs the pacer could dispatch. Two invariants make
 * the derived backlog trustworthy:
 *
 *  1. **Settled-only.** A window is emitted ONLY once its last included day is at
 *     least `settleDays` before today (`window_end ≤ today − settleDays`). A
 *     just-closed-but-unsettled period is never emitted — it reappears, with the
 *     SAME `window_id`, only after it settles. This is what keeps late-merged /
 *     late-resolved PRs from being measured before the data stops moving.
 *  2. **Non-overlapping, inclusive.** Because the fetch search is inclusive on
 *     BOTH bounds (`merged:>=start merged:<=end`), windows use inclusive
 *     last-day ends and the next window starts the following day — so no PR is
 *     ever counted in two adjacent windows. Monthly = calendar months
 *     (`[YYYY-MM-01, last-of-month]`); biweekly = fixed 14-day spans anchored on
 *     `start_date`; weekly = fixed 7-day spans.
 *
 * Each candidate carries its canonical `window_id` (window-id.ts), so the derived
 * backlog joins to the durable ledger by identity, not by filename.
 *
 * Pure, no IO in {@link genBacklog} — safe to import from tests. The only IO is
 * the optional CLI at the bottom (reads config, prints candidates as JSON).
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { DEFAULT_SETTLE_DAYS, isoDate } from "./resolve-window.ts";
import { RAW_SCHEMA_VERSION } from "./run-schema.ts";
import {
    parsePacerConfig,
    type Granularity,
    type PacerConfig,
} from "./pacer-schema.ts";
import { cohortOf, computeWindowId, type Cohort } from "./window-id.ts";

const DAY_MS = 86_400_000;

/** One derived candidate window (before ledger/skip subtraction). */
export interface BacklogWindow {
    repo: string;
    windowStart: string;
    windowEnd: string;
    cohort: Cohort;
    /** Resolved PR cap; `0` = uncapped. */
    maxPrs: number;
    windowId: string;
}

export interface GenBacklogOptions {
    /** "today" as epoch ms (injectable clock for deterministic expansion). */
    now: number;
    /** Settle lag in days; defaults to {@link DEFAULT_SETTLE_DAYS}. */
    settleDays?: number;
    /** Raw-fetch schema version; defaults to the current RAW_SCHEMA_VERSION. */
    rawSchemaVersion?: string;
}

/** Add whole days to a YYYY-MM-DD date, returning YYYY-MM-DD (UTC). */
function addDays(date: string, days: number): string {
    return isoDate(Date.parse(`${date}T00:00:00Z`) + days * DAY_MS);
}

/** First day (YYYY-MM-01) of the month containing `date`. */
function firstOfMonth(date: string): string {
    return `${date.slice(0, 7)}-01`;
}

/** Last inclusive day of the month containing `date`. */
function lastOfMonth(date: string): string {
    const d = new Date(`${date}T00:00:00Z`);
    // Day 0 of the *next* month is the last day of this one.
    return isoDate(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0));
}

/** First day of the month after the one containing `date`. */
function firstOfNextMonth(date: string): string {
    const d = new Date(`${date}T00:00:00Z`);
    return isoDate(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1));
}

/** A [start, end] inclusive period, both YYYY-MM-DD. */
interface Period {
    start: string;
    end: string;
}

/**
 * Enumerate the ordered periods for a granularity from `startDate`, stopping once
 * a period's inclusive `end` would exceed `cutoff` (`today − settleDays`). Never
 * emits a trailing partial/unsettled period.
 */
function enumeratePeriods(
    startDate: string,
    granularity: Granularity,
    cutoff: string,
): Period[] {
    const cutoffMs = Date.parse(`${cutoff}T00:00:00Z`);
    const periods: Period[] = [];

    if (granularity === "month") {
        let first = firstOfMonth(startDate);
        for (;;) {
            const end = lastOfMonth(first);
            if (Date.parse(`${end}T00:00:00Z`) > cutoffMs) break;
            periods.push({ start: first, end });
            first = firstOfNextMonth(first);
        }
        return periods;
    }

    // Fixed-span cadences anchored on start_date: biweekly = 14 days, weekly = 7.
    const span = granularity === "biweekly" ? 14 : 7;
    let start = startDate;
    for (;;) {
        const end = addDays(start, span - 1); // inclusive last day
        if (Date.parse(`${end}T00:00:00Z`) > cutoffMs) break;
        periods.push({ start, end });
        start = addDays(start, span);
    }
    return periods;
}

/**
 * Expand a pacer config into every settled candidate `(repo, window)`. The result
 * is `repos × settledPeriods`, ordered repo-major then chronologically.
 */
export function genBacklog(
    config: PacerConfig,
    opts: GenBacklogOptions,
): BacklogWindow[] {
    const settleDays = opts.settleDays ?? DEFAULT_SETTLE_DAYS;
    const rawSchemaVersion = opts.rawSchemaVersion ?? RAW_SCHEMA_VERSION;
    const cutoff = isoDate(opts.now - settleDays * DAY_MS);
    const cohort: Cohort = cohortOf(config.max_prs);

    const periods = enumeratePeriods(
        config.start_date,
        config.granularity,
        cutoff,
    );

    const out: BacklogWindow[] = [];
    for (const repo of config.repos) {
        for (const p of periods) {
            out.push({
                repo,
                windowStart: p.start,
                windowEnd: p.end,
                cohort,
                maxPrs: config.max_prs,
                windowId: computeWindowId({
                    repo,
                    windowStart: p.start,
                    windowEnd: p.end,
                    cohort,
                    rawSchemaVersion,
                }),
            });
        }
    }
    return out;
}

// ---------------------------------------------------------------------------
// CLI.
// ---------------------------------------------------------------------------

function usage(): string {
    return [
        "Usage:",
        "  node scripts/gen-backlog.ts --config pacer/config.json [options]",
        "",
        "Prints every settled candidate window as a JSON array on stdout.",
        "",
        "Options:",
        "  --config <path>      pacer/config.json (required)",
        "  --settle-days <N>    Settle lag in days (default: 14)",
        "  --now-ms <N>         Override clock (epoch ms) for determinism",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            config: { type: "string" },
            "settle-days": { type: "string" },
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
    if (!v.config) throw new Error("--config is required");

    const config = parsePacerConfig(
        JSON.parse(fs.readFileSync(v.config, "utf8")),
    );
    const settleDays =
        v["settle-days"] === undefined
            ? undefined
            : Number.parseInt(v["settle-days"], 10);
    const now = v["now-ms"] === undefined ? Date.now() : Number(v["now-ms"]);

    const windows = genBacklog(config, { now, settleDays });
    process.stdout.write(`${JSON.stringify(windows, null, 2)}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        process.stderr.write(
            `gen-backlog: ${err instanceof Error ? err.message : String(err)}\n`,
        );
        process.exit(1);
    }
}
