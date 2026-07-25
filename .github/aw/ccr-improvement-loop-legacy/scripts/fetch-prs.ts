#!/usr/bin/env node
/**
 * fetch-prs.ts — fetch PRs in a window and cache the raw REST+GraphQL payloads
 * attribution needs: reviews (author + association + state), inline comments
 * (path/line/startLine/originalLine + thread isResolved + reactions), issue
 * comments (+ reactions), and the commit timeline ({sha, files, committedAt}).
 *
 * Contract: full pagination, versioned cache (rawSchemaVersion),
 * documented secondary-rate-limit backoff (utils.ghApiJsonAsync), and
 * edited/deleted comments are immutable once cached (only --refresh refetches).
 * Distinct ID namespaces (review comment vs review body vs issue comment) are
 * preserved, never merged.
 *
 * NOTE: we intentionally do NOT fetch commit→PR mapping (GET
 * /commits/{sha}/pulls). It was unused by every downstream consumer and cost one
 * request per commit. Its only real use would be filtering base-branch/merge
 * commits out of the attribution timeline for repos that allow merge commits; if
 * such a repo is ever targeted, wire that mapping back in (keep a commit only if
 * its introducing PR == this PR) rather than reintroducing it unused.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import type {
    InlineComment,
    IssueComment,
    PullRequestData,
    PullRequestMetadata,
    Reaction,
    ReviewSummary,
    TimelineCommit,
    User,
} from "./types.ts";
import { RAW_SCHEMA_VERSION } from "./run-schema.ts";
import {
    ghApiGraphqlAsync,
    ghApiJsonAsync,
    ghJsonSync,
    makeLogger,
    runWithConcurrency,
} from "./utils.ts";

const log = makeLogger("fetch-prs");

function usage(): string {
    return [
        "Usage:",
        "  node scripts/fetch-prs.ts [options]",
        "",
        "Options:",
        "  --repo <owner/repo>                Override repo; defaults to cwd's git remote",
        "  --state <open|closed|merged|all>   Default: merged",
        "  --min-prs <N>                      Fetch the most recent N settled PRs (default: 50)",
        "  --settle-days <N>                  Only PRs merged >= N days ago (default: 14)",
        "  --limit <N>                        Hard cap on PRs listed (default: 50)",
        "  --window-start <YYYY-MM-DD>        merged:>=<date> (explicit override)",
        "  --window-end <YYYY-MM-DD>          merged:<=<date> (explicit override)",
        "  --number <N>                       Explicit PR number (repeatable)",
        "  --concurrency <N>                  Max parallel fetches (default: 6)",
        "  --cache-dir <path>                 Override fetch cache dir",
        "  --refresh                          Re-fetch cached PRs (overrides immutability)",
        "  --json                             Machine-readable result to stdout",
        "  --quiet / --verbose",
        "  -h, --help",
    ].join("\n");
}

interface Options {
    repo?: string;
    state: string;
    limit: string;
    minPrs?: string;
    settleDays?: string;
    windowStart?: string;
    windowEnd?: string;
    numbers: string[];
    concurrency: string;
    cacheDir?: string;
    refresh: boolean;
    json: boolean;
    verbose: boolean;
    quiet: boolean;
}

function parseArgs(argv: string[]): Options {
    const parsed = nodeParseArgs({
        args: argv,
        options: {
            repo: { type: "string" },
            state: { type: "string", default: "merged" },
            limit: { type: "string", default: "50" },
            "min-prs": { type: "string" },
            "settle-days": { type: "string", default: "14" },
            "window-start": { type: "string" },
            "window-end": { type: "string" },
            number: { type: "string", multiple: true },
            concurrency: { type: "string", default: "6" },
            "cache-dir": { type: "string" },
            refresh: { type: "boolean", default: false },
            json: { type: "boolean", default: false },
            verbose: { type: "boolean", default: false },
            quiet: { type: "boolean", default: false },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });

    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        process.exit(0);
    }

    const numbers = parsed.values.number ?? [];
    for (const n of numbers) {
        if (!/^\d+$/.test(n)) throw new Error(`invalid --number "${n}"`);
    }

    return {
        repo: parsed.values.repo,
        state: parsed.values.state,
        limit: parsed.values.limit,
        minPrs: parsed.values["min-prs"],
        settleDays: parsed.values["settle-days"],
        windowStart: parsed.values["window-start"],
        windowEnd: parsed.values["window-end"],
        numbers,
        concurrency: parsed.values.concurrency,
        cacheDir: parsed.values["cache-dir"],
        refresh: parsed.values.refresh,
        json: parsed.values.json,
        verbose: parsed.values.verbose,
        quiet: parsed.values.quiet,
    };
}

interface RawUser {
    login: string;
    type: "User" | "Bot";
}
interface RepoView {
    nameWithOwner: string;
}
interface ListedPr {
    number: number;
    title: string;
    author: { login: string } | null;
    mergedAt: string | null;
    createdAt: string | null;
    url: string;
    state: string;
    isDraft?: boolean;
    additions?: number;
    deletions?: number;
    labels?: { name: string }[];
}

function resolveRepo(repo?: string): string {
    if (repo) {
        if (!/^[^/\s]+\/[^/\s]+$/.test(repo)) {
            throw new Error(`invalid --repo "${repo}"; expected owner/repo`);
        }
        return repo;
    }
    const resolved = ghJsonSync<RepoView>([
        "repo",
        "view",
        "--json",
        "nameWithOwner",
    ]);
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- external JSON boundary
    const repoName = resolved.nameWithOwner?.trim() ?? "";
    if (repoName.length === 0)
        throw new Error("gh repo view returned no repo name");
    return repoName;
}

function slimUser(u: RawUser | null): User | null {
    return u ? { login: u.login, type: u.type } : null;
}

function resolveCacheDir(repo: string, cacheDir?: string): string {
    const [owner, repoName] = repo.split("/");
    return cacheDir ?? path.join("pr-cache", `${owner}-${repoName}`);
}

function buildSearch(opts: Options): string {
    const parts: string[] = [];
    if (opts.windowStart) parts.push(`merged:>=${opts.windowStart}`);
    if (opts.windowEnd) {
        parts.push(`merged:<=${opts.windowEnd}`);
    } else if (opts.settleDays) {
        const days = Number(opts.settleDays);
        if (Number.isFinite(days) && days > 0) {
            const cutoff = new Date(Date.now() - days * 86_400_000)
                .toISOString()
                .slice(0, 10);
            parts.push(`merged:<=${cutoff}`);
        }
    }
    return parts.join(" ");
}

function mapReactions(
    raw:
        | { content: string; user: RawUser | null }[]
        | Record<string, unknown>
        | undefined
        | null,
): Reaction[] {
    if (raw == null) return [];
    // GitHub's REST comment payloads inline `reactions` as a SUMMARY OBJECT
    // ({ total_count, "+1", heart, ... }), not an array. The actual reaction
    // list lives at a separate endpoint, but the only downstream consumer (the
    // agent judge, via filtered.json) needs just the content, so expand each
    // content key whose count > 0 into a content-only Reaction (user unknown → null).
    if (Array.isArray(raw)) {
        return raw.map((r) => ({
            content: r.content,
            user: slimUser(r.user),
        }));
    }
    const contentKeys = [
        "+1",
        "-1",
        "laugh",
        "hooray",
        "confused",
        "heart",
        "rocket",
        "eyes",
    ];
    const out: Reaction[] = [];
    for (const key of contentKeys) {
        const count = raw[key];
        if (typeof count === "number" && count > 0) {
            out.push({ content: key, user: null });
        }
    }
    return out;
}

interface RawReview {
    id: number;
    state: string;
    body: string;
    submitted_at: string | null;
    user: RawUser | null;
    author_association?: string;
}
interface RawInline {
    id: number;
    path?: string;
    line?: number;
    start_line?: number | null;
    original_line?: number | null;
    body: string;
    diff_hunk?: string;
    in_reply_to_id?: number;
    pull_request_review_id?: number;
    created_at?: string;
    user: RawUser | null;
    author_association?: string;
    reactions?:
        { content: string; user: RawUser | null }[] | Record<string, unknown>;
}
interface RawIssue {
    id: number;
    body: string;
    created_at: string;
    user: RawUser | null;
    author_association?: string;
    reactions?:
        { content: string; user: RawUser | null }[] | Record<string, unknown>;
}
interface RawCommit {
    sha: string;
    commit: { committer?: { date?: string }; author?: { date?: string } };
}
interface RawCommitFiles {
    files?: { filename: string; patch?: string }[];
}
interface RawPrFull {
    number: number;
    title: string;
    user: RawUser | null;
    html_url: string;
    state: string;
    draft?: boolean;
    additions?: number;
    deletions?: number;
    created_at: string | null;
    merged_at: string | null;
    labels?: { name: string }[];
}

/** GraphQL: resolved-state of inline review threads, keyed by databaseId. */
interface ThreadGraphQl {
    data?: {
        repository?: {
            pullRequest?: {
                reviewThreads?: {
                    nodes?: {
                        isResolved: boolean;
                        comments?: { nodes?: { databaseId: number }[] };
                    }[];
                };
                closingIssuesReferences?: {
                    nodes?: {
                        number: number;
                        labels?: { nodes?: { name: string }[] };
                    }[];
                };
            };
        };
    };
}

async function fetchThreadResolution(
    repo: string,
    number: number,
): Promise<{
    resolvedByCommentId: Map<number, boolean>;
    linkedIssues: { number: number; labels: string[] }[];
}> {
    const [owner, name] = repo.split("/");
    const query = `query($owner:String!,$name:String!,$num:Int!){repository(owner:$owner,name:$name){pullRequest(number:$num){reviewThreads(first:100){nodes{isResolved comments(first:1){nodes{databaseId}}}} closingIssuesReferences(first:20){nodes{number labels(first:20){nodes{name}}}}}}}`;
    const resolvedByCommentId = new Map<number, boolean>();
    const linkedIssues: { number: number; labels: string[] }[] = [];
    try {
        const res = await ghApiGraphqlAsync<ThreadGraphQl>([
            "-f",
            `query=${query}`,
            "-F",
            `owner=${owner}`,
            "-F",
            `name=${name}`,
            "-F",
            `num=${number}`,
        ]);
        const pr = res.data?.repository?.pullRequest;
        for (const t of pr?.reviewThreads?.nodes ?? []) {
            const first = t.comments?.nodes?.[0];
            if (first) resolvedByCommentId.set(first.databaseId, t.isResolved);
        }
        for (const i of pr?.closingIssuesReferences?.nodes ?? []) {
            linkedIssues.push({
                number: i.number,
                labels: (i.labels?.nodes ?? []).map((l) => l.name),
            });
        }
    } catch (err) {
        log.info(
            `thread resolution unavailable for #${number}: ${String(err)}`,
        );
    }
    return { resolvedByCommentId, linkedIssues };
}

async function fetchPrToCache(
    repo: string,
    cacheDir: string,
    refresh: boolean,
    number: number,
): Promise<{ number: number; file: string; cacheHit: boolean }> {
    const outFile = path.join(cacheDir, `pr-${number}.json`);
    if (fs.existsSync(outFile) && !refresh) {
        log.info(`cache hit ${outFile}`);
        return { number, file: outFile, cacheHit: true };
    }
    fs.mkdirSync(cacheDir, { recursive: true });

    const base = `repos/${repo}`;
    // Independent per-PR reads run concurrently; the shared ghRequestLimiter
    // (utils.ts) bounds total in-flight requests so parallel workers × this
    // fan-out never exceed GitHub's secondary-rate-limit concurrency ceiling.
    const [prRaw, reviewsRaw, inlineRaw, issueRaw, commitsRaw, threadRes] =
        await Promise.all([
            ghApiJsonAsync<RawPrFull>(`${base}/pulls/${number}`),
            ghApiJsonAsync<RawReview[]>(`${base}/pulls/${number}/reviews`),
            ghApiJsonAsync<RawInline[]>(`${base}/pulls/${number}/comments`),
            ghApiJsonAsync<RawIssue[]>(`${base}/issues/${number}/comments`),
            ghApiJsonAsync<RawCommit[]>(`${base}/pulls/${number}/commits`),
            fetchThreadResolution(repo, number),
        ]);
    const { resolvedByCommentId, linkedIssues } = threadRes;

    // Per-commit detail (files/patches) is fetched concurrently too; Promise.all
    // preserves commit order and the limiter keeps the burst bounded.
    const commits: TimelineCommit[] = await Promise.all(
        commitsRaw.map(async (c): Promise<TimelineCommit> => {
            const filesRes = await ghApiJsonAsync<RawCommitFiles>(
                `${base}/commits/${c.sha}`,
            );
            const files = filesRes.files ?? [];
            const patches = Object.fromEntries(
                files
                    .filter((f) => f.patch != null)
                    .map((f) => [f.filename, f.patch ?? ""]),
            );
            return {
                sha: c.sha,
                committedAt:
                    c.commit.committer?.date ?? c.commit.author?.date ?? null,
                files: files.map((f) => f.filename),
                ...(Object.keys(patches).length > 0 ? { patches } : {}),
            };
        }),
    );

    const meta: PullRequestMetadata = {
        number: prRaw.number,
        title: prRaw.title,
        author: slimUser(prRaw.user),
        url: prRaw.html_url,
        state: prRaw.state,
        isDraft: prRaw.draft ?? false,
        additions: prRaw.additions,
        deletions: prRaw.deletions,
        createdAt: prRaw.created_at,
        mergedAt: prRaw.merged_at,
        labels: (prRaw.labels ?? []).map((l) => l.name),
        linkedIssues,
    };

    const payload: PullRequestData = {
        rawSchemaVersion: RAW_SCHEMA_VERSION,
        pr: meta,
        reviews: reviewsRaw.map((r): ReviewSummary => ({
            id: r.id,
            state: r.state,
            body: r.body,
            submittedAt: r.submitted_at,
            user: slimUser(r.user),
            authorAssociation: r.author_association,
        })),
        inline: inlineRaw.map((c): InlineComment => ({
            id: c.id,
            path: c.path,
            line: c.line,
            startLine: c.start_line ?? null,
            originalLine: c.original_line ?? null,
            body: c.body,
            diffHunk: c.diff_hunk,
            inReplyToId: c.in_reply_to_id,
            pullRequestReviewId: c.pull_request_review_id,
            createdAt: c.created_at,
            user: slimUser(c.user),
            authorAssociation: c.author_association,
            lineStale: c.line == null && c.original_line != null,
            threadResolved: resolvedByCommentId.get(c.id) ?? false,
            reactions: mapReactions(c.reactions),
        })),
        issue: issueRaw.map((c): IssueComment => ({
            id: c.id,
            body: c.body,
            createdAt: c.created_at,
            user: slimUser(c.user),
            authorAssociation: c.author_association,
            reactions: mapReactions(c.reactions),
        })),
        commits,
    };

    fs.writeFileSync(outFile, JSON.stringify(payload, null, 2));
    log.info(
        `wrote ${outFile} (reviews=${payload.reviews.length} inline=${payload.inline.length} issue=${payload.issue.length} commits=${payload.commits.length})`,
    );
    return { number, file: outFile, cacheHit: false };
}

function listFromSearch(opts: Options): ListedPr[] {
    const args = [
        "pr",
        "list",
        "--repo",
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- set in main()
        opts.repo!,
        "--state",
        opts.state,
        "--limit",
        opts.minPrs ?? opts.limit,
        "--json",
        "number,title,author,mergedAt,createdAt,url,state,isDraft,additions,deletions,labels",
    ];
    const search = buildSearch(opts);
    if (search) args.push("--search", search);
    log.info(`gh ${args.join(" ")}`);
    const prs = ghJsonSync<ListedPr[]>(args);
    log.info(`${prs.length} PR(s) found`);
    return prs;
}

async function main(): Promise<void> {
    const opts = parseArgs(process.argv.slice(2));
    log.enabled = opts.verbose && !opts.quiet;
    const repo = resolveRepo(opts.repo);
    opts.repo = repo;
    const cacheDir = resolveCacheDir(repo, opts.cacheDir);

    const numbers =
        opts.numbers.length > 0
            ? opts.numbers.map((n) => Number(n))
            : listFromSearch(opts).map((p) => p.number);

    const fetched = await runWithConcurrency(
        numbers,
        Number(opts.concurrency),
        (n) => fetchPrToCache(repo, cacheDir, opts.refresh, n),
    );

    const result = {
        repo,
        cacheDir,
        count: fetched.length,
        cacheHits: fetched.filter((f) => f.cacheHit).length,
        fetched,
    };
    if (opts.json) {
        process.stdout.write(JSON.stringify(result, null, 2) + "\n");
    } else {
        process.stderr.write(
            `fetched ${result.count} PR(s) (${result.cacheHits} cache hits) → ${cacheDir}\n`,
        );
    }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((err: unknown) => {
        log.error(err instanceof Error ? err.message : String(err));
        process.exit(1);
    });
}

export { buildSearch, mapReactions, resolveCacheDir };
