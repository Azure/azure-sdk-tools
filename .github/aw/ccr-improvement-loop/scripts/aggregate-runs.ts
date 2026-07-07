#!/usr/bin/env node
/**
 * aggregate-runs.ts — on-demand trend view (low-maintenance V1).
 *
 * Globs all `run-*.json`, validates each against the run schema, de-dups by `run.id`
 * (newest `generatedAt` wins — supersede, never merge), and answers first-order trend
 * queries rebuilt entirely from the source-of-truth files (no DB, never runs in the
 * weekly producing path). Unknown future `schemaVersion` values and malformed/partial
 * files are skipped with a warning rather than aborting the aggregate.
 *
 * Aggregation math is pure + unit-tested; globbing/reading files is IO in `main`.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { parseRun, SCHEMA_VERSION } from "./run-schema.ts";
import type { RunJson } from "./run-schema.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("aggregate-runs");

export interface TrendPoint {
    runId: string;
    repo: string;
    generatedAt: string;
    value: number | null;
}

export interface SeverityTrendPoint {
    runId: string;
    generatedAt: string;
    /** severity → usefulRate value (null when no denominator). */
    bySeverity: Record<string, number | null>;
}

export interface RepoRate {
    repo: string;
    value: number | null;
}

export interface Aggregation {
    runsScanned: number;
    runsKept: number;
    runsSkipped: number;
    dateSpan: { earliest: string | null; latest: string | null };
    missRateOverTime: TrendPoint[];
    verifiedMissRateOverTime: TrendPoint[];
    addressedRateBySeverityOverTime: SeverityTrendPoint[];
    verifiedMissRateByRepo: RepoRate[];
}

/**
 * De-duplicate runs by `run.id`, keeping the one with the latest `generatedAt`.
 * Stable: input order otherwise preserved for equal ids resolved by timestamp.
 */
export function dedupeRuns(runs: RunJson[]): RunJson[] {
    const byId = new Map<string, RunJson>();
    for (const r of runs) {
        const existing = byId.get(r.run.id);
        if (
            !existing ||
            Date.parse(r.run.generatedAt) >=
                Date.parse(existing.run.generatedAt)
        ) {
            byId.set(r.run.id, r);
        }
    }
    return [...byId.values()];
}

function sortByTime(runs: RunJson[]): RunJson[] {
    return [...runs].sort(
        (a, b) =>
            Date.parse(a.run.generatedAt) - Date.parse(b.run.generatedAt) ||
            a.run.id.localeCompare(b.run.id),
    );
}

/**
 * Build the trend aggregation from a set of already-parsed runs. Pure. De-dups by
 * `run.id` and orders time series by `generatedAt`. `verifiedMissRateByRepo` reports
 * the latest run per repo.
 */
export function aggregate(
    runs: RunJson[],
    skipped = 0,
    scanned = runs.length + skipped,
): Aggregation {
    const deduped = sortByTime(dedupeRuns(runs));

    const missRateOverTime: TrendPoint[] = deduped.map((r) => ({
        runId: r.run.id,
        repo: r.run.repo,
        generatedAt: r.run.generatedAt,
        value: r.metrics.rates.missRate?.value ?? null,
    }));

    const verifiedMissRateOverTime: TrendPoint[] = deduped.map((r) => ({
        runId: r.run.id,
        repo: r.run.repo,
        generatedAt: r.run.generatedAt,
        value: r.metrics.rates.verifiedMissRate?.value ?? null,
    }));

    const addressedRateBySeverityOverTime: SeverityTrendPoint[] = deduped.map(
        (r) => {
            const metric = r.metrics.rates.addressedRate;
            const bySeverity: Record<string, number | null> = {};
            for (const slice of metric?.slices ?? []) {
                if (slice.severity != null) {
                    bySeverity[slice.severity] = slice.value;
                }
            }
            return {
                runId: r.run.id,
                generatedAt: r.run.generatedAt,
                bySeverity,
            };
        },
    );

    // Latest run per repo drives the by-repo verified-miss rate.
    const latestByRepo = new Map<string, RunJson>();
    for (const r of deduped) latestByRepo.set(r.run.repo, r); // deduped is time-sorted
    const verifiedMissRateByRepo: RepoRate[] = [...latestByRepo.entries()]
        .map(([repo, r]) => ({
            repo,
            value: r.metrics.rates.verifiedMissRate?.value ?? null,
        }))
        .sort((a, b) => a.repo.localeCompare(b.repo));

    const times = deduped.map((r) => r.run.generatedAt);
    return {
        runsScanned: scanned,
        runsKept: deduped.length,
        runsSkipped: skipped,
        dateSpan: {
            earliest: times[0] ?? null,
            latest: times.at(-1) ?? null,
        },
        missRateOverTime,
        verifiedMissRateOverTime,
        addressedRateBySeverityOverTime,
        verifiedMissRateByRepo,
    };
}

// ---------------------------------------------------------------------------
// IO wiring.
// ---------------------------------------------------------------------------

/** Read + validate one run file. Returns null (with a warning) on any problem. */
function loadRun(file: string): RunJson | null {
    let text: string;
    try {
        text = fs.readFileSync(file, "utf8");
    } catch {
        log.error(`skipping ${file}: unreadable`);
        return null;
    }
    let raw: unknown;
    try {
        raw = JSON.parse(text);
    } catch {
        log.error(`skipping ${file}: invalid JSON`);
        return null;
    }
    const version = (raw as { schemaVersion?: unknown }).schemaVersion;
    if (version !== SCHEMA_VERSION) {
        log.error(
            `skipping ${file}: unsupported schemaVersion ${String(version)} (expected ${SCHEMA_VERSION})`,
        );
        return null;
    }
    try {
        return parseRun(raw);
    } catch (err: unknown) {
        log.error(
            `skipping ${file}: ${err instanceof Error ? err.message.split("\n")[0] : String(err)}`,
        );
        return null;
    }
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/aggregate-runs.ts --glob 'runs/run-*.json' [--out <trends.json>]",
        "",
        "Options:",
        "  --glob <pattern>   Run JSON files (repeatable via positionals too)",
        "  --out <path>       Write the aggregation JSON here (else stdout)",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            glob: { type: "string" },
            out: { type: "string" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: true,
        strict: true,
    });
    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    const v = parsed.values;
    const files = [...parsed.positionals];
    if (v.glob) files.push(...fs.globSync(v.glob, { withFileTypes: false }));
    if (files.length === 0)
        throw new Error("no run files (use --glob or paths)");

    const runs: RunJson[] = [];
    let skipped = 0;
    for (const file of files) {
        const run = loadRun(file);
        if (run) runs.push(run);
        else skipped += 1;
    }

    const result = aggregate(runs, skipped, files.length);
    log.error(
        `scanned ${String(result.runsScanned)}, kept ${String(result.runsKept)}, skipped ${String(result.runsSkipped)}; span ${result.dateSpan.earliest ?? "n/a"} … ${result.dateSpan.latest ?? "n/a"}`,
    );

    const text = JSON.stringify(result, null, 2);
    if (v.out) {
        fs.writeFileSync(v.out, text);
        log.error(`wrote ${v.out}`);
    } else {
        process.stdout.write(text + "\n");
    }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        log.error(err instanceof Error ? err.message : String(err));
        process.exit(1);
    }
}
