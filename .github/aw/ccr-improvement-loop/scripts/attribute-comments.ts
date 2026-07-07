#!/usr/bin/env node
/**
 * attribute-comments.ts — join filtered comments to deterministic, queryable
 * facts: authorKind, (path, line range), and the `ccrSawCode` eligibility gate
 * (did CCR review the code version a human ask anchors to?). Judge-filled
 * verdicts (ccrOutcome, ccrAddressedConcern) default to null here.
 *
 * Inputs: the raw PR cache (source of truth for comments, commits, review
 * timestamps) + the kept-comment set from filtered.json (noise already dropped).
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { isExcludedPath, loadConfig } from "./config.ts";
import type { Config } from "./config.ts";
import { inferCommentKind } from "./filter-comments.ts";
import type {
    AttributedComment,
    AuthorKind,
    InlineComment,
    PullRequestData,
} from "./types.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("attribute-comments");

function authorKindOf(login: string | undefined, cfg: Config): AuthorKind {
    if (!login) return "human";
    const lower = login.toLowerCase();
    if (cfg.ccrLogins.some((l) => l.toLowerCase() === lower)) return "ccr";
    if (/\[bot\]$/i.test(login)) return "bot";
    if (cfg.automationLogins.some((l) => l.toLowerCase() === lower))
        return "bot";
    return "human";
}

/** Inline line range: [start, end] from anchors; null when no line anchor. */
function lineRange(c: InlineComment): {
    start: number | null;
    end: number | null;
} {
    const end = c.line ?? c.originalLine ?? null;
    const start = c.startLine ?? end;
    return { start, end };
}

export function makeFindingId(
    pr: number,
    login: string | undefined,
    path: string | null,
    lineStart: number | null,
    lineEnd: number | null,
): string {
    return `${pr}:${login ?? "unknown"}:${path ?? "_"}:${lineStart ?? "_"}-${lineEnd ?? "_"}`;
}

/** CCR "review event" timestamps: CCR review submissions + inline comments. */
function ccrReviewEventTimes(data: PullRequestData, cfg: Config): number[] {
    const times: number[] = [];
    for (const r of data.reviews) {
        if (authorKindOf(r.user?.login, cfg) === "ccr" && r.submittedAt) {
            const t = Date.parse(r.submittedAt);
            if (!Number.isNaN(t)) times.push(t);
        }
    }
    for (const c of data.inline) {
        if (authorKindOf(c.user?.login, cfg) === "ccr" && c.createdAt) {
            const t = Date.parse(c.createdAt);
            if (!Number.isNaN(t)) times.push(t);
        }
    }
    return times;
}

/**
 * Deterministic eligibility gate: did CCR review the code version a human ask
 * anchors to? True only if CCR reviewed the PR AND at least one CCR review
 * happened at or after the latest commit that touched the ask's file at/before
 * the human comment, but no later than the human comment. Path-less asks fall
 * back to "CCR reviewed the PR before the ask".
 * Never true for non-human/non-ask rows or excluded paths.
 */
function computeCcrSawCode(
    params: {
        authorKind: AuthorKind;
        kind: AttributedComment["kind"];
        path: string | null;
        pathExcluded: boolean;
        createdAt: string | null;
    },
    data: PullRequestData,
    ccrTimes: number[],
): boolean {
    if (params.authorKind !== "human" || params.kind !== "ask") return false;
    if (ccrTimes.length === 0) return false;
    if (params.pathExcluded) return false;

    const commentTs = params.createdAt ? Date.parse(params.createdAt) : NaN;
    if (Number.isNaN(commentTs)) return false;
    const priorCcrTimes = ccrTimes.filter((t) => t <= commentTs);
    if (priorCcrTimes.length === 0) return false;
    if (!params.path) return true;

    let latestPathCommit = Number.NEGATIVE_INFINITY;
    for (const commit of data.commits) {
        if (commit.committedAt == null) continue;
        if (!commit.files.includes(params.path)) continue;
        const t = Date.parse(commit.committedAt);
        if (Number.isNaN(t)) continue;
        if (Number.isNaN(commentTs) || t <= commentTs) {
            if (t > latestPathCommit) latestPathCommit = t;
        }
    }
    if (latestPathCommit === Number.NEGATIVE_INFINITY) {
        latestPathCommit = data.pr.createdAt
            ? Date.parse(data.pr.createdAt)
            : Number.NEGATIVE_INFINITY;
    }
    return priorCcrTimes.some((t) => t >= latestPathCommit);
}

/**
 * Attribute one PR's comments. `keptIds` is the set of comment external ids that
 * survived noise filtering; only those become rows. CCR review events across the
 * raw data drive the deterministic `ccrSawCode` gate.
 */
export function attributePr(
    data: PullRequestData,
    keptIds: Set<number>,
    cfg: Config,
): AttributedComment[] {
    const prNumber = data.pr.number;
    const prUrl = data.pr.url;
    const ccrTimes = ccrReviewEventTimes(data, cfg);

    const out: AttributedComment[] = [];

    const pushRow = (params: {
        externalId: number;
        url: string | undefined;
        login: string | undefined;
        kind: AttributedComment["kind"];
        source: AttributedComment["source"];
        path: string | null;
        lineStart: number | null;
        lineEnd: number | null;
        lineStale: boolean;
        createdAt: string | null;
        body: string;
    }): void => {
        const authorKind = authorKindOf(params.login, cfg);
        const pathExcluded = isExcludedPath(params.path, cfg.excludedPaths);
        const ccrSawCode = computeCcrSawCode(
            {
                authorKind,
                kind: params.kind,
                path: params.path,
                pathExcluded,
                createdAt: params.createdAt,
            },
            data,
            ccrTimes,
        );

        out.push({
            pr: prNumber,
            externalId: params.externalId,
            url: params.url,
            findingId: makeFindingId(
                prNumber,
                params.login,
                params.path,
                params.lineStart,
                params.lineEnd,
            ),
            authorKind,
            authorLogin: params.login,
            kind: params.kind,
            source: params.source,
            path: params.path,
            lineStart: params.lineStart,
            lineEnd: params.lineEnd,
            lineStale: params.lineStale,
            createdAt: params.createdAt,
            ccrSawCode,
            pathExcluded,
            isSubstantive: null,
            diffDetectable: null,
            severity: null,
            category: null,
            confidence: null,
            judgeStatus: null,
            judgeError: null,
            ccrOutcome: null,
            ccrAddressedConcern: null,
            isGap: null,
            theme: null,
            body: params.body,
        });
    };

    for (const c of data.inline) {
        if (!keptIds.has(c.id)) continue;
        const r = lineRange(c);
        // A threaded reply, or an author-reply phrase, is a reply — not an ask.
        const kind =
            c.inReplyToId != null
                ? "reply"
                : inferCommentKind("inline", c.body);
        pushRow({
            externalId: c.id,
            url: prUrl ? `${prUrl}#discussion_r${c.id}` : undefined,
            login: c.user?.login,
            kind,
            source: "inline",
            path: c.path ?? null,
            lineStart: r.start,
            lineEnd: r.end,
            lineStale: c.lineStale ?? false,
            createdAt: c.createdAt ?? null,
            body: c.body,
        });
    }
    for (const r of data.reviews) {
        if (!keptIds.has(r.id)) continue;
        if (!r.body.trim()) continue;
        pushRow({
            externalId: r.id,
            url: prUrl ? `${prUrl}#pullrequestreview-${r.id}` : undefined,
            login: r.user?.login,
            kind: "summary",
            source: "review",
            path: null,
            lineStart: null,
            lineEnd: null,
            lineStale: false,
            createdAt: r.submittedAt,
            body: r.body,
        });
    }
    for (const c of data.issue) {
        if (!keptIds.has(c.id)) continue;
        pushRow({
            externalId: c.id,
            url: prUrl ? `${prUrl}#issuecomment-${c.id}` : undefined,
            login: c.user?.login,
            kind: inferCommentKind("issue", c.body),
            source: "issue",
            path: null,
            lineStart: null,
            lineEnd: null,
            lineStale: false,
            createdAt: c.createdAt,
            body: c.body,
        });
    }

    return out;
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/attribute-comments.ts --glob 'pr-cache/<owner>-<repo>/pr-*.json' --filtered <cache>/filtered.json [options]",
        "",
        "Options:",
        "  --glob <pattern>         Raw PR cache files",
        "  --filtered <path>        filtered.json (kept-comment set)",
        "  --config <path>          Override config.json",
        "  --cache-dir <path>       Write attributed.json here (else stdout)",
        "  --json                   Emit machine-readable result to stdout",
        "  -h, --help",
    ].join("\n");
}

interface FilteredFile {
    comments: { id: number | undefined }[];
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            glob: { type: "string" },
            filtered: { type: "string" },
            config: { type: "string" },
            "cache-dir": { type: "string" },
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

    const cfg = loadConfig(parsed.values.config);
    const files = [...parsed.positionals];
    if (parsed.values.glob) {
        files.push(
            ...fs.globSync(parsed.values.glob, { withFileTypes: false }),
        );
    }
    if (files.length === 0)
        throw new Error("no input files (pass paths or --glob)");

    // Kept-comment set: if --filtered is given, restrict to those ids; else keep all.
    let keptIds: Set<number> | null = null;
    if (parsed.values.filtered) {
        const filtered = JSON.parse(
            fs.readFileSync(parsed.values.filtered, "utf8"),
        ) as FilteredFile;
        keptIds = new Set(
            filtered.comments
                .map((c) => c.id)
                .filter((id): id is number => id != null),
        );
    }

    const rows: AttributedComment[] = [];
    for (const file of files) {
        const data = JSON.parse(
            fs.readFileSync(file, "utf8"),
        ) as PullRequestData;
        const allIds = new Set<number>([
            ...data.inline.map((c) => c.id),
            ...data.reviews.map((r) => r.id),
            ...data.issue.map((c) => c.id),
        ]);
        rows.push(...attributePr(data, keptIds ?? allIds, cfg));
    }

    const payload = {
        processed: rows.length,
        kept: rows.length,
        skipped: 0,
        skipReasons: {
            pathExcluded: rows.filter((r) => r.pathExcluded).length,
        },
        comments: rows,
    };

    if (parsed.values["cache-dir"]) {
        fs.mkdirSync(parsed.values["cache-dir"], { recursive: true });
        const out = `${parsed.values["cache-dir"]}/attributed.json`;
        fs.writeFileSync(out, JSON.stringify(payload, null, 2));
        log.error(`wrote ${out} (${rows.length} attributed comments)`);
    }
    if (parsed.values.json || !parsed.values["cache-dir"]) {
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
