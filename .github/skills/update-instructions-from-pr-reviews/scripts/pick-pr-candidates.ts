#!/usr/bin/env node
/**
 * pick-pr-candidates.ts — Choose the next PRs to test from cached PR JSON files.
 *
 * Reads fetch-prs cache files and ranks PRs by human feedback counts so you can
 * run replay-pull-request in small waves (test a few, validate quality, then continue).
 */

import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import type { PullRequestData } from "./types.ts";
import { isHumanUser, makeLogger, parsePositiveInt } from "./utils.ts";

const log = makeLogger("pick-pr-candidates");

function usage(): string {
    return [
        "Usage:",
        '  node scripts/pick-pr-candidates.ts --glob "pr-cache/<owner>-<repo>/pr-*.json" [options]',
        "",
        "Options:",
        "  --glob <pattern>        Input cache-file glob (required)",
        "  --exclude <list>        Space/comma-separated PR numbers to skip",
        "  --exclude-file <path>   File containing PR numbers (space/comma/newline separated)",
        "  --min-inline <N>        Minimum human inline comments (default: 1)",
        "  --min-total <N>         Minimum human total comments (default: 1)",
        "  --limit <N>             Max candidates to return (default: 10)",
        "  --format summary|json   Output format (default: summary)",
        "  -h, --help              Show this help",
    ].join("\n");
}

function parseCli(argv: string[]): Options {
    const parsed = parseArgs({
        args: argv,
        options: {
            glob: { type: "string" },
            exclude: { type: "string" },
            "exclude-file": { type: "string" },
            "min-inline": { type: "string", default: "1" },
            "min-total": { type: "string", default: "1" },
            limit: { type: "string", default: "10" },
            format: { type: "string", default: "summary" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });

    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        process.exit(0);
    }

    const glob = parsed.values.glob;
    if (!glob) throw new Error("--glob is required");

    const format = parsed.values.format;
    if (format !== "summary" && format !== "json") {
        throw new Error(
            `--format must be summary|json; got ${JSON.stringify(parsed.values.format)}`,
        );
    }

    const excludeNumbers = new Set<number>();
    if (parsed.values.exclude) {
        for (const n of parseNumbers(parsed.values.exclude))
            excludeNumbers.add(n);
    }
    if (parsed.values["exclude-file"]) {
        for (const n of readExcludeFile(parsed.values["exclude-file"]))
            excludeNumbers.add(n);
    }

    return {
        glob,
        exclude: excludeNumbers,
        minInline: parsePositiveInt(
            parsed.values["min-inline"],
            "--min-inline",
        ),
        minTotal: parsePositiveInt(parsed.values["min-total"], "--min-total"),
        limit: parsePositiveInt(parsed.values.limit, "--limit"),
        format,
    };
}

interface Options {
    glob: string;
    exclude: Set<number>;
    minInline: number;
    minTotal: number;
    limit: number;
    format: "summary" | "json";
}

export interface Candidate {
    pr: number;
    title: string;
    url: string;
    mergedAt: string | null;
    author: string;
    humanInline: number;
    humanReview: number;
    humanIssue: number;
    humanTotal: number;
}

function parseNumbers(text: string): number[] {
    return text
        .split(/[\s,]+/)
        .map((t) => t.trim())
        .filter(Boolean)
        .filter((t) => /^\d+$/.test(t))
        .map((t) => Number.parseInt(t, 10));
}

function readExcludeFile(filePath: string): number[] {
    try {
        const raw = fs.readFileSync(filePath, "utf8");
        return parseNumbers(raw);
    } catch (err) {
        throw new Error(
            `cannot read --exclude-file ${filePath}: ${(err as Error).message}`,
        );
    }
}

export function summarizePrData(data: PullRequestData): Candidate {
    const humanReview = data.reviews.filter((c) => isHumanUser(c.user)).length;
    const humanInline = data.inline.filter((c) => isHumanUser(c.user)).length;
    const humanIssue = data.issue.filter((c) => isHumanUser(c.user)).length;

    return {
        pr: data.pr.number,
        title: data.pr.title,
        url: data.pr.url,
        mergedAt: data.pr.mergedAt,
        author: data.pr.author?.login ?? "unknown",
        humanInline,
        humanReview,
        humanIssue,
        humanTotal: humanInline + humanReview + humanIssue,
    };
}

export function selectCandidates(all: Candidate[], opts: Options): Candidate[] {
    return all
        .filter((c) => !opts.exclude.has(c.pr))
        .filter((c) => c.humanInline >= opts.minInline)
        .filter((c) => c.humanTotal >= opts.minTotal)
        .sort((a, b) => {
            if (b.humanInline !== a.humanInline)
                return b.humanInline - a.humanInline;
            if (b.humanTotal !== a.humanTotal)
                return b.humanTotal - a.humanTotal;
            return b.pr - a.pr;
        })
        .slice(0, opts.limit);
}

function loadPrData(filePath: string): PullRequestData | null {
    try {
        const raw = fs.readFileSync(filePath, "utf8");
        return JSON.parse(raw) as PullRequestData;
    } catch {
        return null;
    }
}

function printSummary(candidates: Candidate[], scanned: number): void {
    process.stdout.write(`Scanned: ${scanned} cache files\n`);
    process.stdout.write(`Candidates: ${candidates.length}\n\n`);
    for (const c of candidates) {
        process.stdout.write(
            `#${c.pr} inline=${c.humanInline} review=${c.humanReview} issue=${c.humanIssue} total=${c.humanTotal} @${c.author}\n`,
        );
        process.stdout.write(`  ${c.title}\n`);
        process.stdout.write(`  ${c.url}\n`);
    }
}

function main(): void {
    const opts = parseCli(process.argv.slice(2));

    const files = fs.globSync(opts.glob, { withFileTypes: false });
    if (files.length === 0) {
        throw new Error(`no files matched --glob ${JSON.stringify(opts.glob)}`);
    }

    const all: Candidate[] = [];
    for (const file of files) {
        const data = loadPrData(file);
        if (!data) continue;
        all.push(summarizePrData(data));
    }

    const candidates = selectCandidates(all, opts);

    if (opts.format === "json") {
        process.stdout.write(
            JSON.stringify(
                {
                    scanned: files.length,
                    excluded: opts.exclude.size,
                    filters: {
                        minInline: opts.minInline,
                        minTotal: opts.minTotal,
                        limit: opts.limit,
                    },
                    candidates,
                },
                null,
                2,
            ) + "\n",
        );
        return;
    }

    printSummary(candidates, files.length);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        const message = err instanceof Error ? err.message : String(err);
        log.error(message);
        process.exit(1);
    }
}
