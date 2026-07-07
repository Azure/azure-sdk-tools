#!/usr/bin/env node
/**
 * build-judge-input.ts — deterministic evidence pack for the agent judge.
 *
 * Joins attributed comments back to the raw PR cache so Step 3 gets exactly the
 * bounded evidence promised by references/judge.prompt.md: comment body, minimal
 * diff hunk, same-PR CCR comments for gap candidates, post-comment patches, and
 * direct author replies for CCR comments. This script prepares inputs only; it
 * never assigns judge verdicts.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import type {
    AttributedComment,
    InlineComment,
    JudgeInputItem,
    PullRequestData,
} from "./types.ts";
import { makeLogger, parsePositiveInt } from "./utils.ts";

const log = makeLogger("build-judge-input");

interface AttributedFile {
    comments: AttributedComment[];
}

function clip(value: string | undefined, maxChars: number): string {
    if (!value) return "";
    return value.length <= maxChars ? value : value.slice(0, maxChars);
}

function rawInlineById(data: PullRequestData): Map<number, InlineComment> {
    return new Map(data.inline.map((c) => [c.id, c]));
}

function changedAfterComment(
    data: PullRequestData,
    comment: AttributedComment,
    maxChars: number,
): string {
    if (!comment.path || !comment.createdAt) return "";
    const commentTs = Date.parse(comment.createdAt);
    if (Number.isNaN(commentTs)) return "";

    const patches: string[] = [];
    for (const commit of data.commits) {
        if (!commit.committedAt || !commit.files.includes(comment.path)) {
            continue;
        }
        const commitTs = Date.parse(commit.committedAt);
        if (Number.isNaN(commitTs) || commitTs <= commentTs) continue;
        const patch = commit.patches?.[comment.path];
        if (patch) patches.push(patch);
    }
    return clip(patches.join("\n"), maxChars);
}

function authorReplies(
    data: PullRequestData,
    comment: AttributedComment,
    maxChars: number,
): string[] {
    const prAuthor = data.pr.author?.login;
    if (!prAuthor) return [];
    const replies = data.inline.filter(
        (c) =>
            c.inReplyToId === comment.externalId &&
            c.user?.login === prAuthor,
    );
    return replies.map((c) => clip(c.body.trim(), maxChars));
}

function ccrCommentsForPr(
    comments: AttributedComment[],
    candidate: AttributedComment,
    maxChars: number,
): NonNullable<JudgeInputItem["ccrComments"]> {
    const candidateTs = candidate.createdAt ? Date.parse(candidate.createdAt) : NaN;
    return comments
        .filter((c) => {
            if (c.pr !== candidate.pr || c.authorKind !== "ccr" || c.pathExcluded) {
                return false;
            }
            const ccrTs = c.createdAt ? Date.parse(c.createdAt) : NaN;
            return !Number.isNaN(candidateTs) && !Number.isNaN(ccrTs) && ccrTs <= candidateTs;
        })
        .map((c) => ({
            path: c.path,
            lineStart: c.lineStart,
            lineEnd: c.lineEnd,
            body: clip(c.body.trim(), maxChars),
        }));
}

export function buildJudgeInputForPr(
    data: PullRequestData,
    comments: AttributedComment[],
    opts: { maxBodyChars: number; maxDiffChars: number },
): JudgeInputItem[] {
    const inlineById = rawInlineById(data);
    const items: JudgeInputItem[] = [];

    for (const c of comments.filter((row) => row.pr === data.pr.number)) {
        if (c.pathExcluded) continue;
        const rawInline = inlineById.get(c.externalId);
        const base = {
            id: c.findingId,
            body: clip(c.body.trim(), opts.maxBodyChars),
            diffHunk: clip(rawInline?.diffHunk, opts.maxDiffChars),
            path: c.path,
            lineStart: c.lineStart,
            lineEnd: c.lineEnd,
            lineStale: c.lineStale,
        };

        if (c.authorKind === "human" && c.kind === "ask") {
            items.push({
                ...base,
                purpose: "gap-candidate",
                ccrComments: ccrCommentsForPr(
                    comments,
                    c,
                    opts.maxBodyChars,
                ),
            });
        } else if (c.authorKind === "ccr") {
            items.push({
                ...base,
                purpose: "ccr-comment",
                postCommentDiff: changedAfterComment(
                    data,
                    c,
                    opts.maxDiffChars,
                ),
                authorReplies: authorReplies(data, c, opts.maxBodyChars),
            });
        }
    }

    return items;
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/build-judge-input.ts --glob 'pr-cache/<owner>-<repo>/pr-*.json' --attributed <cache>/attributed.json [options]",
        "",
        "Options:",
        "  --glob <pattern>         Raw PR cache files",
        "  --attributed <path>      attributed.json (comments[])",
        "  --cache-dir <path>       Write judge-input.json here (else stdout)",
        "  --max-body-chars <N>     Per-comment body budget (default: 2000)",
        "  --max-diff-chars <N>     Per-diff budget (default: 4000)",
        "  --json                   Emit machine-readable result to stdout",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            glob: { type: "string" },
            attributed: { type: "string" },
            "cache-dir": { type: "string" },
            "max-body-chars": { type: "string", default: "2000" },
            "max-diff-chars": { type: "string", default: "4000" },
            json: { type: "boolean", default: false },
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
    if (!v.attributed) throw new Error("--attributed is required");

    const files = [...parsed.positionals];
    if (v.glob) files.push(...fs.globSync(v.glob, { withFileTypes: false }));
    if (files.length === 0)
        throw new Error("no raw PR cache files (pass paths or --glob)");

    const attributed = JSON.parse(
        fs.readFileSync(v.attributed, "utf8"),
    ) as AttributedFile;
    const opts = {
        maxBodyChars: parsePositiveInt(
            v["max-body-chars"] ?? "2000",
            "--max-body-chars",
        ),
        maxDiffChars: parsePositiveInt(
            v["max-diff-chars"] ?? "4000",
            "--max-diff-chars",
        ),
    };

    const items: JudgeInputItem[] = [];
    for (const file of files) {
        const data = JSON.parse(
            fs.readFileSync(file, "utf8"),
        ) as PullRequestData;
        items.push(...buildJudgeInputForPr(data, attributed.comments, opts));
    }

    const payload = { processed: items.length, items };
    if (v["cache-dir"]) {
        fs.mkdirSync(v["cache-dir"], { recursive: true });
        const out = `${v["cache-dir"]}/judge-input.json`;
        fs.writeFileSync(out, JSON.stringify(payload, null, 2));
        log.error(`wrote ${out} (${String(items.length)} judge input items)`);
    }
    if (v.json || !v["cache-dir"]) {
        process.stdout.write(JSON.stringify(payload, null, 2) + "\n");
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