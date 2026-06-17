#!/usr/bin/env node
/**
 * format-comments.ts — Render the JSON output of filter-comments.ts as compact
 * human+LLM-readable markdown. Only diff-hunk changed lines (-/+) are included,
 * which keeps token count low while preserving context.
 */

import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";

import type { KeptComment } from "./types.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("format-comments");

function usage(): string {
    return [
        "Usage:",
        "  node scripts/format-comments.ts --input kept.json",
        '  node scripts/filter-comments.ts --glob "pr-cache/owner-repo/pr-*.json" | node scripts/format-comments.ts',
        "",
        "Options:",
        "  --input <file>          Read from file instead of stdin",
        "  --no-hunk               Omit diff_hunk blocks entirely",
        "  --max-hunk-lines <N>    Truncate changed hunk lines to N (default: 10)",
        "  -h, --help              Show this help",
    ].join("\n");
}

export function parseCli(argv: string[]): Options {
    const parsed = parseArgs({
        args: argv,
        options: {
            input: { type: "string" },
            "no-hunk": { type: "boolean", default: false },
            "max-hunk-lines": { type: "string", default: "10" },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });

    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        process.exit(0);
    }

    const maxHunkLines = Number.parseInt(parsed.values["max-hunk-lines"], 10);
    if (!Number.isFinite(maxHunkLines) || maxHunkLines < 1) {
        throw new Error(`--max-hunk-lines must be a positive integer`);
    }

    return {
        input: parsed.values.input,
        includeHunk: !parsed.values["no-hunk"],
        maxHunkLines,
    };
}

interface FilterOutput {
    kept: KeptComment[];
}

interface Options {
    input?: string;
    includeHunk: boolean;
    maxHunkLines: number;
}

function readInput(opts: Options): FilterOutput {
    let raw: string;
    if (opts.input) {
        try {
            raw = fs.readFileSync(opts.input, "utf8");
        } catch (e) {
            throw new Error(
                `cannot read ${opts.input}: ${(e as Error).message}`,
            );
        }
    } else {
        raw = fs.readFileSync("/dev/stdin", "utf8");
    }

    try {
        return JSON.parse(raw) as FilterOutput;
    } catch (e) {
        throw new Error(`invalid JSON: ${(e as Error).message}`);
    }
}

/**
 * Extract only the changed lines (-/+) from a diff hunk, dropping @@-headers
 * and unchanged context lines. Truncates to maxLines if needed.
 */
export function extractChangedLines(hunk: string, maxLines: number): string[] {
    const lines = hunk.split(/\r?\n/);
    const changed = lines.filter((l) => l.startsWith("-") || l.startsWith("+"));
    if (changed.length > maxLines) {
        const truncated = changed.slice(0, maxLines);
        truncated.push(`… (${changed.length - maxLines} more lines)`);
        return truncated;
    }
    return changed;
}

export function formatHunk(
    hunk: string | undefined,
    includeHunk: boolean,
    maxHunkLines: number,
): string {
    if (!includeHunk || !hunk || hunk.trim().length === 0) return "";
    const lines = extractChangedLines(hunk, maxHunkLines);
    if (lines.length === 0) return "";
    return lines.map((l) => `> ${l}`).join("\n") + "\n\n";
}

export function formatComment(
    c: KeptComment,
    includeHunk: boolean,
    maxHunkLines: number,
): string {
    const parts: string[] = [];

    // Header: reviewer (linked to deep-link URL when available) + location
    const location =
        c.path != null
            ? `${c.path}${c.line != null ? `:${c.line}` : ""}`
            : undefined;
    const userTag = c.comment_url
        ? `[[${c.user ?? "unknown"}](${c.comment_url})]`
        : `[${c.user ?? "unknown"}]`;
    const header = location ? `### ${userTag} ${location}` : `### ${userTag}`;
    parts.push(header);

    // Hunk (inline comments only — review-level comments have no hunk)
    const hunkBlock = formatHunk(c.diff_hunk, includeHunk, maxHunkLines);
    if (hunkBlock) parts.push(hunkBlock.trimEnd());

    // Comment body
    parts.push(c.body);

    return parts.join("\n\n");
}

function main(): void {
    const opts = parseCli(process.argv.slice(2));
    const data = readInput(opts);

    // data.kept comes from external JSON; guard even though the type says non-null.
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON boundary
    if (!data.kept || data.kept.length === 0) {
        process.stderr.write("format-comments: no comments to format\n");
        return;
    }

    // Group by PR number
    const byPr = new Map<number | undefined, KeptComment[]>();
    for (const c of data.kept) {
        const key = c.pr;
        const bucket = byPr.get(key) ?? [];
        bucket.push(c);
        byPr.set(key, bucket);
    }

    const out: string[] = [];

    for (const [prNum, comments] of byPr) {
        const firstWithUrl = comments.find((c) => c.url);
        const prHeader =
            prNum != null
                ? firstWithUrl?.url
                    ? `## PR #${prNum}: [${firstWithUrl.url}](${firstWithUrl.url})`
                    : `## PR #${prNum}`
                : `## PR (unknown)`;
        out.push(prHeader);

        for (const c of comments) {
            out.push(formatComment(c, opts.includeHunk, opts.maxHunkLines));
            out.push("---");
        }
    }

    process.stdout.write(out.join("\n\n") + "\n");
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
