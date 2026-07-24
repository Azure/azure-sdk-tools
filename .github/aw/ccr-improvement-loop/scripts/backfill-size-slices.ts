#!/usr/bin/env node
/**
 * backfill-size-slices.ts — additively add PR-size (`sizeBucket`) coverage slices
 * to already-emitted `run-*.json` files.
 *
 * The `sizeBucket` coverage breakdown (Phase 5) is derived deterministically from
 * each PR's already-captured `additions`/`deletions`, so historical runs can gain
 * it with **no agent re-run** — exactly the recompute the plan promises. This is an
 * additive, self-verifying migration: it recomputes metrics from the file's own
 * `prs`/`comments`, asserts the recomputed `ccrCoverage` value/denominator and its
 * `prType` cells are byte-identical to what the file already stores (proving the
 * eligible-PR set was reproduced exactly), then appends only the new size cells.
 * If the recompute disagrees, the file is left untouched and reported — never
 * silently rewritten. Idempotent: a file that already has size cells is skipped.
 *
 * Usage:
 *   node scripts/backfill-size-slices.ts --dir <runs-dir> [--dir <dir> ...]
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { computeMetrics } from "./compute-metrics.ts";
import { loadConfig } from "./config.ts";
import { parseRun } from "./run-schema.ts";
import type { Metric } from "./run-schema.ts";
import type { PrRowOut } from "./pr-metrics.ts";
import type { AttributedComment } from "./types.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("backfill-size-slices");

type Cells = NonNullable<Metric["slices"]>;

interface RunFile {
    run?: { ccrEnabledSince?: string | null };
    prs?: PrRowOut[];
    comments?: AttributedComment[];
    metrics?: { rates?: { ccrCoverage?: Metric } };
    [k: string]: unknown;
}

export type BackfillStatus = "added" | "already-present" | "inconsistent";

export interface BackfillResult {
    status: BackfillStatus;
    /** New size cells that were (or would be) appended. */
    sizeCells: Cells;
    /** Populated when status is "inconsistent". */
    detail?: string;
}

const prTypeCells = (m: Metric): Cells =>
    (m.slices ?? []).filter((s) => s.prType != null && s.sizeBucket == null);
const sizeCellsOf = (m: Metric): Cells =>
    (m.slices ?? []).filter((s) => s.sizeBucket != null);

/**
 * Compute the additive size cells for one run object, gated by a self-consistency
 * check. Pure (no I/O) so it is unit-testable. Does NOT mutate `run`; the caller
 * splices `sizeCells` into `run.metrics.rates.ccrCoverage.slices` on success.
 */
export function planBackfill(
    run: RunFile,
    automationLogins: string[],
): BackfillResult {
    const cov = run.metrics?.rates?.ccrCoverage;
    if (!cov) {
        return {
            status: "inconsistent",
            sizeCells: [],
            detail: "no ccrCoverage",
        };
    }
    if (sizeCellsOf(cov).length > 0) {
        return { status: "already-present", sizeCells: [] };
    }
    const recomputed = computeMetrics(run.prs ?? [], run.comments ?? [], {
        ccrEnabledSince: run.run?.ccrEnabledSince ?? null,
        automationLogins,
    }).rates.ccrCoverage;
    if (!recomputed) {
        return {
            status: "inconsistent",
            sizeCells: [],
            detail: "recompute produced no ccrCoverage",
        };
    }
    // Self-verify: the eligible-PR set must be reproduced exactly, else the size
    // cells would partition a different population than the stored prType cells.
    const valEq =
        cov.value === recomputed.value ||
        (cov.value != null &&
            recomputed.value != null &&
            Math.abs(cov.value - recomputed.value) < 1e-9);
    const denEq = cov.denominator === recomputed.denominator;
    const prTypeEq =
        JSON.stringify(prTypeCells(cov)) ===
        JSON.stringify(prTypeCells(recomputed));
    if (!valEq || !denEq || !prTypeEq) {
        return {
            status: "inconsistent",
            sizeCells: [],
            detail: `recompute mismatch (value=${String(valEq)} denom=${String(
                denEq,
            )} prTypeCells=${String(prTypeEq)})`,
        };
    }
    return { status: "added", sizeCells: sizeCellsOf(recomputed) };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/backfill-size-slices.ts --dir <runs-dir> [--dir <dir> ...]",
        "",
        "Options:",
        "  --dir <path>   Directory of run-*.json to backfill (repeatable)",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const { values } = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            dir: { type: "string", multiple: true },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    if (values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    const dirs = values.dir ?? [];
    if (dirs.length === 0) throw new Error("at least one --dir is required");
    const automationLogins = loadConfig().automationLogins;

    let added = 0;
    let skipped = 0;
    let failed = 0;
    for (const dir of dirs) {
        const files = fs
            .readdirSync(dir)
            .filter((f) => f.startsWith("run-") && f.endsWith(".json"));
        for (const f of files) {
            const p = path.join(dir, f);
            const raw: unknown = JSON.parse(fs.readFileSync(p, "utf8"));
            const run = raw as RunFile;
            const result = planBackfill(run, automationLogins);
            if (result.status === "already-present") {
                skipped++;
                continue;
            }
            if (result.status === "inconsistent") {
                failed++;
                log.error(`SKIP ${f}: ${result.detail ?? "inconsistent"}`);
                continue;
            }
            const cov = run.metrics?.rates?.ccrCoverage;
            if (!cov) continue;
            cov.slices = [...(cov.slices ?? []), ...result.sizeCells];
            // Re-validate the whole file against the schema before writing.
            parseRun(run);
            fs.writeFileSync(p, `${JSON.stringify(run, null, 2)}\n`);
            added++;
        }
    }
    log.error(
        `backfill complete: ${String(added)} updated, ${String(
            skipped,
        )} already had size cells, ${String(failed)} skipped (inconsistent)`,
    );
    if (failed > 0) process.exitCode = 1;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
