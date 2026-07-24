#!/usr/bin/env node
/**
 * emit-run-json.ts — assemble the run JSON (the system of record) and
 * validate it against run-schema.ts on write.
 *
 * `run.id` is the canonical window identity (see window-id.ts) — it encodes repo,
 * window bounds, cohort, and raw schema version — and is the idempotency key:
 * re-emitting the same window overwrites `run-<id>.json` and is **content-stable**
 * — every field is byte-identical except `generatedAt` (the only wall-clock
 * value). `canonicalizeRun` strips that one volatile field so golden/equivalence
 * tests can byte-compare two emits; the Step 6 supersede rule ("newest
 * generatedAt wins for a duplicate run.id") stays well-defined.
 *
 * `buildRunJson` is a pure function (no IO) so it can be unit-tested for
 * schema-validity and content-stability; `main` wires file IO around it.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { loadConfig } from "./config.ts";
import { computeMetrics } from "./compute-metrics.ts";
import { derivePrRow } from "./pr-metrics.ts";
import type { PrRowOut } from "./pr-metrics.ts";
import {
    parseRun,
    SCHEMA_VERSION,
    type CommentRow,
    type Experiment,
    type RunJson,
} from "./run-schema.ts";
import type {
    AttributedComment,
    ProposedEdit,
    PrType,
    PrTypeSource,
    PullRequestData,
    Theme,
} from "./types.ts";
import { makeLogger } from "./utils.ts";
import {
    computeWindowId,
    ownerRepoOf,
    runArtifactBase,
    type Cohort,
} from "./window-id.ts";

// Re-exported for callers/tests that resolve identity helpers via this module.
export { ownerRepoOf, computeWindowId, type Cohort };

const log = makeLogger("emit-run-json");

/** Run metadata supplied by the pipeline driver (everything but id/prCount/generatedAt). */
export interface RunMetaInput {
    /** "owner/repo". */
    repo: string;
    windowStart: string;
    windowEnd: string;
    windowLagDays: number;
    prState: string;
    model: string;
    modelTool: string;
    temperature: number;
    matchedCcrLogin: string | null;
    promptHashes: Record<string, string>;
    vocabularyHash: string | null;
    toolVersion: string;
    ccrEnabledSince: string | null;
    /**
     * PR-selection cohort for this window. Absent ⇒ uncapped (the default full
     * cohort). Feeds `run.id` (window identity) but is NOT persisted as its own
     * run-meta field — the cohort is recoverable from `run.id`.
     */
    cohort?: Cohort;
}

export interface BuildRunInput {
    meta: RunMetaInput;
    prs: PrRowOut[];
    comments: AttributedComment[];
    themes: Theme[];
    proposedEdits: ProposedEdit[];
    experiment: Experiment | null;
    automationLogins: string[];
    /** Wall-clock emit timestamp; injectable so tests are deterministic. */
    generatedAt: string;
}

/** Strip volatile/oversized fields (body) from an attributed comment → CommentRow. */
function toCommentRow(c: AttributedComment): CommentRow {
    return {
        pr: c.pr,
        externalId: c.externalId,
        url: c.url ?? null,
        rowId: c.rowId,
        findingId: c.findingId,
        authorKind: c.authorKind,
        authorLogin: c.authorLogin ?? null,
        kind: c.kind,
        source: c.source,
        path: c.path,
        lineStart: c.lineStart,
        lineEnd: c.lineEnd,
        lineStale: c.lineStale,
        createdAt: c.createdAt,
        isSubstantive: c.isSubstantive,
        diffDetectable: c.diffDetectable,
        severity: c.severity,
        category: c.category,
        confidence: c.confidence,
        judgeStatus: c.judgeStatus,
        judgeError: c.judgeError,
        ccrSawCode: c.ccrSawCode,
        pathExcluded: c.pathExcluded,
        ccrOutcome: c.ccrOutcome,
        ccrAddressedConcern: c.ccrAddressedConcern,
        isGap: c.isGap,
        theme: c.theme,
    };
}

export function runIdOf(meta: {
    repo: string;
    windowStart: string;
    windowEnd: string;
    cohort?: Cohort;
}): string {
    return computeWindowId({
        repo: meta.repo,
        windowStart: meta.windowStart,
        windowEnd: meta.windowEnd,
        cohort: meta.cohort ?? "uncapped",
    });
}

/** Pure assembly + validation. Throws if the result violates run-schema. */
export function buildRunJson(input: BuildRunInput): RunJson {
    const { meta } = input;
    const metrics = computeMetrics(input.prs, input.comments, {
        ccrEnabledSince: meta.ccrEnabledSince,
        automationLogins: input.automationLogins,
    });

    const run: RunJson = {
        schemaVersion: SCHEMA_VERSION,
        run: {
            id: runIdOf(meta),
            repo: meta.repo,
            windowStart: meta.windowStart,
            windowEnd: meta.windowEnd,
            windowLagDays: meta.windowLagDays,
            prState: meta.prState,
            prCount: input.prs.length,
            model: meta.model,
            modelTool: meta.modelTool,
            temperature: meta.temperature,
            matchedCcrLogin: meta.matchedCcrLogin,
            promptHashes: meta.promptHashes,
            vocabularyHash: meta.vocabularyHash,
            toolVersion: meta.toolVersion,
            ccrEnabledSince: meta.ccrEnabledSince,
            generatedAt: input.generatedAt,
        },
        prs: input.prs,
        comments: input.comments.map(toCommentRow),
        themes: input.themes,
        metrics,
        proposedEdits: input.proposedEdits,
        experiment: input.experiment,
    };

    // Validate on write — the schema is load-bearing.
    return parseRun(run);
}

/** Sentinel used in place of the wall-clock `generatedAt` in canonical form. */
export const CANONICAL_GENERATED_AT = "<canonical>";

/**
 * Return a deep copy of a run with volatile fields normalized, for deterministic
 * equality/golden assertions. `generatedAt` (the sole wall-clock value) is the
 * only volatile field today; canonicalization is the equality contract two emits
 * of the same window must satisfy.
 */
export function canonicalizeRun(run: RunJson): RunJson {
    const copy = structuredClone(run);
    copy.run.generatedAt = CANONICAL_GENERATED_AT;
    return copy;
}

/**
 * Resolve the emit timestamp. Injectable via `CCR_GENERATED_AT` so runs are
 * reproducible/deterministic in CI and tests; defaults to wall-clock.
 */
export function resolveGeneratedAt(
    env: NodeJS.ProcessEnv = process.env,
): string {
    return env.CCR_GENERATED_AT ?? new Date().toISOString();
}

// ---------------------------------------------------------------------------
// IO wiring.
// ---------------------------------------------------------------------------

interface ClassifiedFile {
    prs: {
        number: number;
        prType?: PrType | null;
        prTypeSource?: PrTypeSource;
        classificationStatus?: "complete" | "needs-agent" | "failed";
    }[];
}
interface AttributedFile {
    comments: AttributedComment[];
}

// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is an ergonomic cast for callers
function readJson<T>(file: string): T {
    return JSON.parse(fs.readFileSync(file, "utf8")) as T;
}

function readOptionalArray<T>(file: string | undefined, key: string): T[] {
    if (!file) return [];
    const parsed = readJson<Record<string, unknown>>(file);
    const arr = parsed[key];
    return Array.isArray(arr) ? (arr as T[]) : [];
}

function printSummary(run: RunJson): void {
    const lines: string[] = [
        `run ${run.run.id} — ${String(run.run.prCount)} PRs`,
    ];
    for (const [name, m] of Object.entries(run.metrics.rates)) {
        const v = m.value == null ? "n/a" : m.value.toFixed(3);
        lines.push(
            `  ${name.padEnd(22)} ${v.padStart(7)}  (${String(m.numerator ?? "-")}/${String(m.denominator ?? "-")})`,
        );
    }
    for (const w of run.metrics.coverageWarnings) lines.push(`  ! ${w}`);
    process.stderr.write(lines.join("\n") + "\n");
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/emit-run-json.ts --meta meta.json --classified classified.json \\",
        "    --attributed attributed.json --glob 'pr-cache/<o>-<r>/pr-*.json' --out-dir runs/ [options]",
        "",
        "Options:",
        "  --meta <path>            Run metadata JSON (RunMetaInput)",
        "  --classified <path>      classified.json (PR classifications)",
        "  --attributed <path>      attributed.json (comments[])",
        "  --glob <pattern>         Raw PR cache files (for derived PR metrics)",
        "  --themes <path>          themes.json (optional)",
        "  --proposed-edits <path>  proposedEdits.json (optional)",
        "  --experiment <path>      experiment.json (optional)",
        "  --config <path>          Override config.json",
        "  --out-dir <path>         Write run-<id>.json here (else stdout)",
        "  --print-summary          Print headline metrics table to stderr",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            meta: { type: "string" },
            classified: { type: "string" },
            attributed: { type: "string" },
            glob: { type: "string" },
            themes: { type: "string" },
            "proposed-edits": { type: "string" },
            experiment: { type: "string" },
            config: { type: "string" },
            "out-dir": { type: "string" },
            "print-summary": { type: "boolean", default: false },
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
    if (!v.meta) throw new Error("--meta is required");
    if (!v.classified) throw new Error("--classified is required");
    if (!v.attributed) throw new Error("--attributed is required");

    const cfg = loadConfig(v.config);
    const meta = readJson<RunMetaInput>(v.meta);
    const classified = readJson<ClassifiedFile>(v.classified);
    const attributed = readJson<AttributedFile>(v.attributed);

    const classByNumber = new Map(classified.prs.map((p) => [p.number, p]));
    const rawFiles = [...parsed.positionals];
    if (v.glob) {
        rawFiles.push(...fs.globSync(v.glob, { withFileTypes: false }));
    }
    if (rawFiles.length === 0) {
        throw new Error("no raw PR cache files (pass paths or --glob)");
    }

    const prs: PrRowOut[] = [];
    for (const file of rawFiles) {
        const data = readJson<PullRequestData>(file);
        const cls = classByNumber.get(data.pr.number);
        prs.push(
            derivePrRow(
                data,
                {
                    prType: cls?.prType ?? null,
                    prTypeSource: cls?.prTypeSource ?? "unknown",
                    classificationStatus: cls?.classificationStatus ?? "failed",
                },
                cfg.ccrLogins,
            ),
        );
    }

    const run = buildRunJson({
        meta,
        prs,
        comments: attributed.comments,
        themes: readOptionalArray<Theme>(v.themes, "themes"),
        proposedEdits: readOptionalArray<ProposedEdit>(
            v["proposed-edits"],
            "proposedEdits",
        ),
        experiment: v.experiment
            ? readJson<{ experiment: Experiment | null }>(v.experiment)
                  .experiment
            : null,
        automationLogins: cfg.automationLogins,
        generatedAt: resolveGeneratedAt(),
    });

    if (v["print-summary"]) printSummary(run);

    const text = JSON.stringify(run, null, 2);
    if (v["out-dir"]) {
        fs.mkdirSync(v["out-dir"], { recursive: true });
        const out = `${v["out-dir"]}/${runArtifactBase(run.run.id)}.json`;
        fs.writeFileSync(out, text);
        log.error(`wrote ${out} (${String(run.run.prCount)} PRs)`);
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
