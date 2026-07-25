#!/usr/bin/env node
/**
 * prep-run.ts — deterministic orchestration for the CCR improvement loop.
 *
 * Runs the prep stages in order (fetch → classify → filter → attribute →
 * build-judge-input → trace) with explicit-arg subprocess calls (never shell
 * string interpolation), then writes meta.json and a deterministic
 * prep-summary.json audit of the normalized cache.
 *
 * Exit is non-zero if a fatal audit check trips (duplicate rowIds or duplicate
 * judge-input ids). Duplicate findingIds are reported as warnings, not fatal,
 * because findingId is a grouping key by design.
 */
import { execFileSync } from "node:child_process";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { loadConfig } from "./config.ts";
import { buildPrepSummary } from "./prep-summary.ts";
import type { PrepSummary } from "./prep-summary.ts";
import type {
    AttributedComment,
    JudgeInputItem,
    PullRequestData,
} from "./types.ts";
import { makeLogger, sha256 } from "./utils.ts";

const log = makeLogger("prep-run");
const here = path.dirname(fileURLToPath(import.meta.url));

function scriptPath(name: string): string {
    return path.join(here, name);
}

function runStage(script: string, args: string[]): void {
    log.error(`stage: ${script} ${args.join(" ")}`);
    execFileSync("node", [scriptPath(script), ...args], {
        stdio: ["ignore", "ignore", "inherit"],
    });
}

interface MetaInput {
    repo: string;
    windowStart: string;
    windowEnd: string;
    windowLagDays: number;
    prState: string;
    matchedCcrLogin: string | null;
    ccrEnabledSince: string | null;
    promptHashes: Record<string, string>;
    vocabularyHash: string | null;
}

/** Minimal run metadata; the agent may later refine the model field. */
export function buildMeta(input: MetaInput): Record<string, unknown> {
    return {
        repo: input.repo,
        windowStart: input.windowStart,
        windowEnd: input.windowEnd,
        windowLagDays: input.windowLagDays,
        prState: input.prState,
        model: "agentic-workflow",
        modelTool: "gh-aw",
        temperature: 0,
        matchedCcrLogin: input.matchedCcrLogin,
        promptHashes: input.promptHashes,
        vocabularyHash: input.vocabularyHash,
        toolVersion: "1.0",
        ccrEnabledSince: input.ccrEnabledSince,
    };
}

/**
 * sha256 of each pinned reference prompt + the controlled vocabulary, for run
 * provenance/reproducibility. Missing files are skipped (hash omitted).
 */
export function computeProvenance(referencesDir: string): {
    promptHashes: Record<string, string>;
    vocabularyHash: string | null;
} {
    const prompts: Record<string, string> = {
        judge: "judge.prompt.md",
        theme: "theme.prompt.md",
        classify: "classify-pr.prompt.md",
    };
    const promptHashes: Record<string, string> = {};
    for (const [key, file] of Object.entries(prompts)) {
        const p = path.join(referencesDir, file);
        if (fs.existsSync(p)) {
            promptHashes[key] = sha256(fs.readFileSync(p, "utf8"));
        }
    }
    const vocabPath = path.join(referencesDir, "controlled-vocabulary.md");
    const vocabularyHash = fs.existsSync(vocabPath)
        ? sha256(fs.readFileSync(vocabPath, "utf8"))
        : null;
    return { promptHashes, vocabularyHash };
}

function readArrayField<T>(file: string, key: string): T[] {
    if (!fs.existsSync(file)) return [];
    const parsed = JSON.parse(fs.readFileSync(file, "utf8")) as Record<
        string,
        unknown
    >;
    const arr = parsed[key];
    return Array.isArray(arr) ? (arr as T[]) : [];
}

/** Assemble the prep summary from the normalized cache written by the stages. */
export function summarizeCache(cacheDir: string, glob: string): PrepSummary {
    const prs = fs
        .globSync(glob, { withFileTypes: false })
        .map(
            (file) =>
                JSON.parse(fs.readFileSync(file, "utf8")) as PullRequestData,
        );
    const attributed = readArrayField<AttributedComment>(
        path.join(cacheDir, "attributed.json"),
        "comments",
    );
    const judgeInput = readArrayField<JudgeInputItem>(
        path.join(cacheDir, "judge-input.json"),
        "items",
    );
    const classified = readArrayField<{
        number: number;
        classificationStatus?: string;
    }>(path.join(cacheDir, "classified.json"), "prs");

    return buildPrepSummary({
        prs,
        attributed,
        judgeInput,
        classified,
    });
}

const DAY_MS = 86_400_000;

/** UTC calendar date (YYYY-MM-DD) for an epoch-millisecond instant. */
export function isoDate(ms: number): string {
    return new Date(ms).toISOString().slice(0, 10);
}

/**
 * Earliest merged/created date (YYYY-MM-DD) across the PR cache — the start of
 * the sampled window. Empty string when no dated PR is present.
 */
export function windowStartOf(glob: string): string {
    let earliest = Number.POSITIVE_INFINITY;
    for (const file of fs.globSync(glob, { withFileTypes: false })) {
        const data = JSON.parse(
            fs.readFileSync(file, "utf8"),
        ) as PullRequestData;
        const stamp = data.pr.mergedAt ?? data.pr.createdAt;
        if (!stamp) continue;
        const t = Date.parse(stamp);
        if (!Number.isNaN(t) && t < earliest) earliest = t;
    }
    if (earliest === Number.POSITIVE_INFINITY) return "";
    return new Date(earliest).toISOString().slice(0, 10);
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/prep-run.ts --repo <owner/repo> --cache-dir <path> [options]",
        "",
        "Options:",
        "  --repo <owner/repo>      Target repository (required)",
        "  --cache-dir <path>       Normalized cache directory (required)",
        "  --state <state>          PR state to fetch (default: merged)",
        "  --settle-days <N>        Settle lag in days (default: 14)",
        "  --window-start <DATE>    Window start YYYY-MM-DD (backfill; default: rolling)",
        "  --window-end <DATE>      Window end YYYY-MM-DD (backfill; default: settle cutoff)",
        "  --window-days <N>        Rolling window length when no --window-start (default: 14)",
        "  --max-prs <N>            Cap PRs per window; 0 = uncapped full cohort (default: 0)",
        "  --config <path>          Override config.json",
        "  --skip-fetch             Reuse existing pr-*.json in cache-dir (offline)",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            repo: { type: "string" },
            "cache-dir": { type: "string" },
            state: { type: "string", default: "merged" },
            "min-prs": { type: "string", default: "50" },
            "settle-days": { type: "string", default: "14" },
            "window-start": { type: "string" },
            "window-end": { type: "string" },
            "window-days": { type: "string", default: "14" },
            "max-prs": { type: "string", default: "0" },
            config: { type: "string" },
            "skip-fetch": { type: "boolean", default: false },
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
    if (!v["cache-dir"]) throw new Error("--cache-dir is required");

    const repo = v.repo;
    const cacheDir = v["cache-dir"];
    const cfg = loadConfig(v.config);
    fs.mkdirSync(cacheDir, { recursive: true });
    const glob = path.join(cacheDir, "pr-*.json");

    const settleDays = Number.parseInt(v["settle-days"], 10);
    const windowDays = Number.parseInt(v["window-days"], 10);
    // Resolve the measurement window. Explicit --window-end/--window-start drive
    // a historical backfill; otherwise fall back to a rolling settled window:
    // end = today - settleDays (the settle cutoff), start = end - windowDays.
    // Passing both bounds explicitly means the fetch is bounded by TIME, so it
    // stays safe even when uncapped.
    const windowEnd =
        v["window-end"] ?? isoDate(Date.now() - settleDays * DAY_MS);
    const windowStart =
        v["window-start"] ??
        isoDate(Date.parse(windowEnd) - windowDays * DAY_MS);

    if (!v["skip-fetch"]) {
        const maxPrs = Number.parseInt(v["max-prs"], 10);
        const fetchArgs = [
            "--repo",
            repo,
            "--state",
            v.state,
            "--window-start",
            windowStart,
            "--window-end",
            windowEnd,
            "--cache-dir",
            cacheDir,
            "--quiet",
        ];
        if (Number.isFinite(maxPrs) && maxPrs > 0) {
            fetchArgs.push("--min-prs", String(maxPrs));
        } else {
            // Uncapped: take the whole window. GitHub search caps at 1000 hits.
            fetchArgs.push("--limit", "1000");
        }
        runStage("fetch-prs.ts", fetchArgs);
    } else {
        log.error("skip-fetch: reusing existing pr-*.json in cache-dir");
    }

    runStage("classify-pr.ts", ["--glob", glob, "--cache-dir", cacheDir]);
    runStage("filter-comments.ts", ["--glob", glob, "--cache-dir", cacheDir]);
    runStage("attribute-comments.ts", [
        "--glob",
        glob,
        "--filtered",
        path.join(cacheDir, "filtered.json"),
        "--cache-dir",
        cacheDir,
    ]);
    runStage("build-judge-input.ts", [
        "--glob",
        glob,
        "--attributed",
        path.join(cacheDir, "attributed.json"),
        "--cache-dir",
        cacheDir,
    ]);

    const provenance = computeProvenance(path.join(here, "..", "references"));
    const meta = buildMeta({
        repo,
        windowStart,
        windowEnd,
        windowLagDays: settleDays,
        prState: v.state,
        matchedCcrLogin: cfg.ccrLogins[0] ?? null,
        ccrEnabledSince: cfg.ccrEnabledSince,
        promptHashes: provenance.promptHashes,
        vocabularyHash: provenance.vocabularyHash,
    });
    fs.writeFileSync(
        path.join(cacheDir, "meta.json"),
        JSON.stringify(meta, null, 2),
    );

    const summary = summarizeCache(cacheDir, glob);
    fs.writeFileSync(
        path.join(cacheDir, "prep-summary.json"),
        JSON.stringify(summary, null, 2),
    );

    log.error(
        `prep-summary: ${String(summary.prCount)} PRs, ${String(summary.commentCount)} comments, ` +
            `${String(summary.ccrInlineCount)} CCR inline, ` +
            `${String(summary.duplicateRowIds)} dup rowIds, ` +
            `${String(summary.duplicateFindingIds)} dup findingIds`,
    );
    for (const w of summary.warnings) log.error(`warning: ${w}`);

    if (summary.fatal) {
        for (const r of summary.fatalReasons) log.error(`FATAL: ${r}`);
        process.exit(1);
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
