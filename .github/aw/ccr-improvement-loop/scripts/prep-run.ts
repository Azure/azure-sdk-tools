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
    VerifiedMiss,
} from "./types.ts";
import { makeLogger } from "./utils.ts";

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
    windowEnd: string;
    windowLagDays: number;
    prState: string;
    matchedCcrLogin: string | null;
    ccrEnabledSince: string | null;
}

/** Minimal run metadata; the agent may later refine model/hashes. */
export function buildMeta(input: MetaInput): Record<string, unknown> {
    return {
        repo: input.repo,
        windowStart: "",
        windowEnd: input.windowEnd,
        windowLagDays: input.windowLagDays,
        prState: input.prState,
        model: "agentic-workflow",
        modelTool: "gh-aw",
        temperature: 0,
        matchedCcrLogin: input.matchedCcrLogin,
        promptHashes: {},
        vocabularyHash: null,
        toolVersion: "1.0",
        ccrEnabledSince: input.ccrEnabledSince,
    };
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

/** Ensure a schema-shaped, empty traced.json exists (trace may fail offline). */
function ensureTracedJson(cacheDir: string): void {
    const out = path.join(cacheDir, "traced.json");
    if (fs.existsSync(out)) return;
    fs.writeFileSync(
        out,
        JSON.stringify(
            {
                processed: 0,
                kept: 0,
                skipped: 0,
                skipReasons: { verified: 0 },
                verifiedMisses: [],
            },
            null,
            2,
        ),
    );
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
    const traced = readArrayField<VerifiedMiss>(
        path.join(cacheDir, "traced.json"),
        "verifiedMisses",
    );

    return buildPrepSummary({
        prs,
        attributed,
        judgeInput,
        classified,
        traced,
    });
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
        "  --min-prs <N>            Minimum settled PRs (default: 50)",
        "  --settle-days <N>        Settle lag in days (default: 14)",
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

    if (!v["skip-fetch"]) {
        const fetchArgs = [
            "--repo",
            repo,
            "--state",
            v.state,
            "--min-prs",
            v["min-prs"],
            "--settle-days",
            v["settle-days"],
            "--cache-dir",
            cacheDir,
            "--quiet",
        ];
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

    // Trace may fail offline / on unsupported repos; always leave valid traced.json.
    try {
        runStage("trace-bug-origin.ts", [
            "--repo",
            repo,
            "--classified",
            path.join(cacheDir, "classified.json"),
            "--glob",
            glob,
            "--cache-dir",
            cacheDir,
        ]);
    } catch (err: unknown) {
        log.error(
            `trace-bug-origin failed (non-fatal): ${err instanceof Error ? err.message : String(err)}`,
        );
    }
    ensureTracedJson(cacheDir);

    const meta = buildMeta({
        repo,
        windowEnd: new Date().toISOString().slice(0, 10),
        windowLagDays: Number.parseInt(v["settle-days"], 10),
        prState: v.state,
        matchedCcrLogin: cfg.ccrLogins[0] ?? null,
        ccrEnabledSince: cfg.ccrEnabledSince,
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
