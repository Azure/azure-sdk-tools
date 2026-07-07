#!/usr/bin/env node
/**
 * filter-comments.ts — drop low-signal noise from cached PR JSON and emit the
 * comments worth analyzing. CCR comments are **kept** (tagged later by
 * attribute-comments) because they are the subject of the usefulness metrics;
 * only non-CCR automation, boilerplate, and human noise are dropped.
 *
 * Each surviving comment is tagged `ask` | `reply` | `summary`. Only `ask`
 * comments count toward "humans asked for X".
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { loadConfig } from "./config.ts";
import type {
    ClassifiableComment,
    CommentKind,
    CommentSource,
    FilterDropReason,
    FilterOpts,
    FilterResult,
    KeptComment,
    PullRequestData,
    User,
} from "./types.ts";
import { isBot, makeLogger } from "./utils.ts";

const log = makeLogger("filter-comments");

function usage(): string {
    return [
        "Usage:",
        "  node scripts/filter-comments.ts <pr-file.json> [more.json ...] [options]",
        '  node scripts/filter-comments.ts --glob "pr-cache/<owner>-<repo>/pr-*.json" [options]',
        "",
        "Options:",
        "  --min-length <N>         Minimum body length to keep (default: 20)",
        "  --no-default-bots        Don't auto-deny known automation accounts",
        "  --include-self           Keep PR-author self comments",
        "  --config <path>          Override config.json",
        "  --cache-dir <path>       Write filtered.json here (else stdout)",
        "  --json                   Emit machine-readable result to stdout",
        "  -h, --help",
    ].join("\n");
}

interface CliOptions extends FilterOpts {
    files: string[];
    glob?: string;
    cacheDir?: string;
    json: boolean;
}

export function parseArgs(argv: string[]): CliOptions {
    const parsed = nodeParseArgs({
        args: argv,
        options: {
            "no-default-bots": { type: "boolean", default: false },
            "include-self": { type: "boolean", default: false },
            "min-length": { type: "string", default: "20" },
            glob: { type: "string" },
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
        process.exit(0);
    }

    const cfg = loadConfig(parsed.values.config);
    const minLength = Number.parseInt(parsed.values["min-length"], 10);
    if (!Number.isFinite(minLength) || minLength < 1) {
        throw new Error(
            `invalid --min-length "${parsed.values["min-length"]}"`,
        );
    }

    const out: CliOptions = {
        files: [...parsed.positionals],
        includeSelf: parsed.values["include-self"],
        minLength,
        glob: parsed.values.glob,
        defaultBots: !parsed.values["no-default-bots"],
        cacheDir: parsed.values["cache-dir"],
        json: parsed.values.json,
        ccrLogins: new Set(cfg.ccrLogins.map((l) => l.toLowerCase())),
        automationLogins: new Set(
            cfg.automationLogins.map((l) => l.toLowerCase()),
        ),
    };

    if (out.glob) {
        out.files.push(...fs.globSync(out.glob, { withFileTypes: false }));
    }
    if (out.files.length === 0) {
        throw new Error("no input files (pass one or more paths, or --glob)");
    }
    return out;
}

/** Author associations kept: people formally joined to the repository. */
export const ALLOWED_ASSOCIATIONS: ReadonlySet<string> = new Set([
    "OWNER",
    "MEMBER",
    "COLLABORATOR",
]);

/** Whole-body low-signal patterns (boilerplate / chatter). */
export const LOW_SIGNAL: RegExp[] = [
    /^lgtm[!.\s]*$/i,
    /^\+1[!.\s]*$/,
    /^thanks?[!.\s]*$/i,
    /^ty[!.\s]*$/i,
    /^done[!.\s]*$/i,
    /^fixed[!.\s]*$/i,
    /^ack(nowledged)?[!.\s]*$/i,
    /^(👍|👎|🎉|❤️|🚀|✅)+$/u,
];

/** Automation marker HTML comments embedded by tooling. */
export const AUTOMATION_MARKERS: RegExp[] = [
    /<!--\s*#comment-cli-pr\s*-->/i,
    /<!--\s*install-instructions\s*-->/i,
];

export function isAutomationBoilerplate(body: string): boolean {
    return AUTOMATION_MARKERS.some((re) => re.test(body));
}

/** Author-reply phrases → tag `reply` not `ask`. */
export const AUTHOR_REPLY_PATTERNS: RegExp[] = [
    /^(fixed|done|addressed|resolved|removed|updated|refactored|reverted|accepted|agreed)\b/i,
    /^(intentional|not taken)\b/i,
    /^this (?:is|was) intentional\b/i,
    /^you'?re right\b/i,
    /^(yes|no),?\s+(added|removed|updated|fixed)\b/i,
    /^(good|nice|great)\s+catch\b/i,
    /^(thanks|thank you)[,!.\s]/i,
    /^(will|i'?ll|going to)\s+(fix|address|update|do)\b/i,
    /^sg[!.\s]/i,
];

export function inferCommentKind(
    source: CommentSource,
    body: string,
): CommentKind {
    if (source === "review") return "summary";
    const trimmed = body.trim();
    if (AUTHOR_REPLY_PATTERNS.some((re) => re.test(trimmed))) return "reply";
    return "ask";
}

export function isQuotedOnly(body: string): boolean {
    const lines = body
        .split(/\r?\n/)
        .map((l) => l.trim())
        .filter(Boolean);
    if (lines.length === 0) return true;
    return lines.every((l) => l.startsWith(">"));
}

function isCcr(
    login: string | undefined,
    ccrLogins: Set<string> | undefined,
): boolean {
    if (!login) return false;
    return ccrLogins?.has(login.toLowerCase()) ?? false;
}

export function classifyComment({
    comment,
    prAuthor,
    opts,
}: {
    comment: ClassifiableComment;
    prAuthor: string | undefined;
    opts: FilterOpts;
}): FilterDropReason {
    const body = (comment.body ?? "").trim();
    const login = comment.user?.login;
    const loginLower = login?.toLowerCase();

    // CCR comments are always kept — they are the subject of usefulness metrics.
    if (isCcr(login, opts.ccrLogins)) return "keep";

    // Non-CCR automation accounts and boilerplate are dropped.
    if (
        opts.defaultBots !== false &&
        loginLower &&
        opts.automationLogins?.has(loginLower)
    ) {
        return "automation";
    }
    if (opts.defaultBots !== false && isAutomationBoilerplate(body)) {
        return "automation";
    }

    // Other (non-CCR) bots.
    if (isBot(comment.user)) return "bot";

    // Human association gate.
    if (
        comment.authorAssociation !== undefined &&
        !ALLOWED_ASSOCIATIONS.has(comment.authorAssociation.toUpperCase())
    ) {
        return "association";
    }

    if (!opts.includeSelf && prAuthor && login === prAuthor) return "self";

    if (body.length < opts.minLength) return "short";
    if (LOW_SIGNAL.some((re) => re.test(body))) return "short";
    if (isQuotedOnly(body)) return "quoted";

    return "keep";
}

export function synthesizeCommentUrl(
    prUrl: string | undefined,
    source: CommentSource,
    id: number | undefined,
    prNumber: number | undefined,
): string | undefined {
    if (!prUrl || id == null || !Number.isFinite(id)) return undefined;
    switch (source) {
        case "inline":
            return `${prUrl}#discussion_r${id}`;
        case "review":
            return `${prUrl}#pullrequestreview-${id}`;
        case "issue": {
            const issuesUrl =
                prNumber != null
                    ? prUrl.replace(/\/pull\/(\d+)(?=$|[/?#])/, `/issues/$1`)
                    : prUrl;
            return `${issuesUrl}#issuecomment-${id}`;
        }
    }
}

function emptyDropped(): FilterResult["dropped"] {
    return {
        bot: 0,
        automation: 0,
        short: 0,
        quoted: 0,
        self: 0,
        association: 0,
        kindFiltered: 0,
        sourceFiltered: 0,
    };
}

export function filterPullRequestData(
    data: PullRequestData,
    opts: FilterOpts,
): FilterResult {
    /* eslint-disable @typescript-eslint/no-unnecessary-condition -- external JSON boundary */
    const prAuthor = data.pr?.author?.login;
    const prNumber = data.pr?.number;
    const prUrl = data.pr?.url;
    /* eslint-enable @typescript-eslint/no-unnecessary-condition */

    const dropped = emptyDropped();
    const kept: KeptComment[] = [];

    interface Sourced {
        source: CommentSource;
        id: number;
        body: string;
        user: User | null;
        authorAssociation?: string;
        path?: string;
        line?: number;
        startLine?: number | null;
        originalLine?: number | null;
        lineStale?: boolean;
        diffHunk?: string;
        createdAt?: string | null;
        threadResolved?: boolean;
        reactions?: KeptComment["reactions"];
    }

    const all: Sourced[] = [
        ...data.reviews
            // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- body may be absent
            .filter((r) => r.body?.trim())
            .map((r): Sourced => ({
                source: "review",
                id: r.id,
                body: r.body,
                user: r.user,
                authorAssociation: r.authorAssociation,
                createdAt: r.submittedAt,
            })),
        ...data.inline.map((c): Sourced => ({
            source: "inline",
            id: c.id,
            body: c.body,
            user: c.user,
            authorAssociation: c.authorAssociation,
            path: c.path,
            line: c.line,
            startLine: c.startLine,
            originalLine: c.originalLine,
            lineStale: c.lineStale,
            diffHunk: c.diffHunk,
            createdAt: c.createdAt,
            threadResolved: c.threadResolved,
            reactions: c.reactions,
        })),
        ...data.issue.map((c): Sourced => ({
            source: "issue",
            id: c.id,
            body: c.body,
            user: c.user,
            authorAssociation: c.authorAssociation,
            createdAt: c.createdAt,
            reactions: c.reactions,
        })),
    ];

    for (const c of all) {
        const verdict = classifyComment({ comment: c, prAuthor, opts });
        if (verdict !== "keep") {
            dropped[verdict]++;
            continue;
        }
        const trimmed = c.body.trim();
        const kind = inferCommentKind(c.source, trimmed);
        if (opts.kinds && !opts.kinds.has(kind)) {
            dropped.kindFiltered++;
            continue;
        }
        kept.push({
            pr: prNumber,
            url: prUrl,
            id: c.id,
            commentUrl: synthesizeCommentUrl(prUrl, c.source, c.id, prNumber),
            source: c.source,
            kind,
            user: c.user?.login,
            authorAssociation: c.authorAssociation,
            path: c.path,
            line: c.line ?? c.originalLine ?? undefined,
            startLine: c.startLine ?? null,
            originalLine: c.originalLine ?? null,
            lineStale: c.lineStale ?? false,
            diffHunk: c.diffHunk,
            body: trimmed,
            createdAt: c.createdAt ?? undefined,
            threadResolved: c.threadResolved ?? false,
            reactions: c.reactions,
        });
    }

    return { kept, dropped };
}

function main(): void {
    const opts = parseArgs(process.argv.slice(2));
    const dropped = emptyDropped();
    const kept: KeptComment[] = [];

    for (const file of opts.files) {
        let data: PullRequestData;
        try {
            data = JSON.parse(fs.readFileSync(file, "utf8")) as PullRequestData;
        } catch (e) {
            throw new Error(`cannot read ${file}: ${(e as Error).message}`);
        }
        const result = filterPullRequestData(data, opts);
        kept.push(...result.kept);
        for (const k of Object.keys(dropped) as (keyof typeof dropped)[]) {
            dropped[k] += result.dropped[k];
        }
    }

    const skipped = (Object.values(dropped) as number[]).reduce(
        (a, b) => a + b,
        0,
    );
    const payload = {
        processed: kept.length + skipped,
        kept: kept.length,
        skipped,
        skipReasons: dropped,
        comments: kept,
    };

    if (opts.cacheDir) {
        fs.mkdirSync(opts.cacheDir, { recursive: true });
        const out = `${opts.cacheDir}/filtered.json`;
        fs.writeFileSync(out, JSON.stringify(payload, null, 2));
        log.error(`wrote ${out} (${kept.length} kept, ${skipped} skipped)`);
    }
    if (opts.json || !opts.cacheDir) {
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
