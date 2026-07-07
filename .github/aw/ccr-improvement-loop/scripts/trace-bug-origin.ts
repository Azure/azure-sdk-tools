#!/usr/bin/env node
/**
 * trace-bug-origin.ts — verified-miss back-trace. For each bug-fix PR, map
 * the fix's removed/changed lines back to **pre-fix file coordinates**, blame
 * them at the **parent of the fix** (removed lines aren't on the current
 * branch), resolve the introducing commit → introducing PR, and grade CCR's
 * *opportunity* on that PR. A verified miss requires CCR to have actually
 * line-reviewed the introducing PR (≥1 inline comment) yet stayed silent on the
 * introducing lines — so a PR CCR never really reviewed is never labeled
 * preventable.
 *
 * The diff-mapping and verdict logic are pure + unit-tested; git blame /
 * commit→PR resolution are IO in `main`.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { isExcludedPath, loadConfig } from "./config.ts";
import type {
    BlameConfidence,
    CcrOpportunity,
    PullRequestData,
    TraceOutcome,
    VerifiedMiss,
} from "./types.ts";
import {
    ghApiJsonPaginatedSync,
    ghApiJsonSync,
    ghJsonSync,
    isBot,
    makeLogger,
} from "./utils.ts";

const log = makeLogger("trace-bug-origin");

/** A line range in a file's coordinates (1-based, inclusive). */
export interface LineRange {
    start: number;
    end: number;
}

/**
 * Parse a unified-diff patch and return the **old-file** line ranges that were
 * removed or changed (the lines that, pre-fix, contained the bug). Pure-addition
 * hunks contribute nothing; a patch with no hunks (pure rename/mode change) is
 * treated as having no traceable ranges.
 */
export function removedPreFixRanges(patch: string | undefined): LineRange[] {
    if (!patch) return [];
    const ranges: LineRange[] = [];
    const lines = patch.split("\n");
    let oldLine = 0;
    let runStart: number | null = null;
    let runEnd: number | null = null;

    const flush = (): void => {
        if (runStart != null && runEnd != null) {
            ranges.push({ start: runStart, end: runEnd });
        }
        runStart = null;
        runEnd = null;
    };

    for (const line of lines) {
        const header = /^@@ -(\d+)(?:,(\d+))? \+\d+(?:,\d+)? @@/.exec(line);
        if (header) {
            flush();
            oldLine = Number.parseInt(header[1] ?? "0", 10);
            continue;
        }
        if (line.startsWith("---") || line.startsWith("+++")) continue;
        // "\ No newline at end of file" marker: metadata, not a code line.
        if (line.startsWith("\\")) continue;
        if (line.startsWith("-")) {
            // Removed line: belongs to a pre-fix range.
            runStart ??= oldLine;
            runEnd = oldLine;
            oldLine += 1;
        } else if (line.startsWith("+")) {
            // Added line: no old-file coordinate; does not break a removed run
            // only if adjacent (a change block is -lines then +lines).
            // Keep the run open so a -/+ change block stays one range.
        } else {
            // Context line (or unparsed): closes any open removed run.
            flush();
            oldLine += 1;
        }
    }
    flush();
    return ranges;
}

export function rangesIntersect(a: LineRange, b: LineRange): boolean {
    return a.start <= b.end && b.start <= a.end;
}

export interface CcrActivity {
    /** CCR inline comments on the introducing PR: file path + line range. */
    inline: { path: string; range: LineRange }[];
    /** CCR posted a review summary / issue comment (no inline required). */
    hasSummary: boolean;
}

export interface OpportunityVerdict {
    ccrOpportunity: CcrOpportunity;
    ccrActiveOnIntroducingPr: boolean;
    ccrCommentedOnLines: boolean;
    verifiedMiss: boolean;
}

/**
 * Grade CCR's opportunity on the introducing PR for a given file + pre-fix
 * ranges. A verified miss requires CCR to have line-reviewed the PR (≥1 inline
 * comment somewhere) but not on the introducing lines. `ccrSummaryOnly` and
 * `ccrInactive` are explicitly NOT misses.
 */
export function gradeOpportunity(
    path: string,
    ranges: LineRange[],
    ccr: CcrActivity,
): OpportunityVerdict {
    const onLines = ccr.inline.some(
        (c) =>
            c.path === path && ranges.some((r) => rangesIntersect(c.range, r)),
    );
    if (onLines) {
        return {
            ccrOpportunity: "ccrCommentedOnLines",
            ccrActiveOnIntroducingPr: true,
            ccrCommentedOnLines: true,
            verifiedMiss: false,
        };
    }
    const onFile = ccr.inline.some((c) => c.path === path);
    if (onFile) {
        return {
            ccrOpportunity: "ccrCommentedOnFile",
            ccrActiveOnIntroducingPr: true,
            ccrCommentedOnLines: false,
            verifiedMiss: true,
        };
    }
    if (ccr.inline.length > 0) {
        return {
            ccrOpportunity: "ccrActiveOnPr",
            ccrActiveOnIntroducingPr: true,
            ccrCommentedOnLines: false,
            verifiedMiss: true,
        };
    }
    if (ccr.hasSummary) {
        return {
            ccrOpportunity: "ccrSummaryOnly",
            ccrActiveOnIntroducingPr: false,
            ccrCommentedOnLines: false,
            verifiedMiss: false,
        };
    }
    return {
        ccrOpportunity: "ccrInactive",
        ccrActiveOnIntroducingPr: false,
        ccrCommentedOnLines: false,
        verifiedMiss: false,
    };
}

/** Map a set of blamed commits → a single introducing PR / trace outcome. */
export function resolveIntroducingPr(prNumbers: number[]): {
    introducedByPr: number | null;
    traceOutcome: TraceOutcome;
} {
    const distinct = [...new Set(prNumbers)];
    if (distinct.length === 0) {
        return { introducedByPr: null, traceOutcome: "unresolved-no-pr" };
    }
    if (distinct.length > 1) {
        return { introducedByPr: null, traceOutcome: "ambiguous-multiple-prs" };
    }
    return { introducedByPr: distinct[0] ?? null, traceOutcome: "resolved" };
}

/** Blame confidence: low when a hunk spans multiple introducing commits/authors. */
export function blameConfidenceOf(
    distinctCommits: number,
    distinctAuthors: number,
): BlameConfidence {
    if (distinctCommits <= 1 && distinctAuthors <= 1) return "high";
    if (distinctAuthors <= 1) return "medium";
    return "low";
}

// ---------------------------------------------------------------------------
// IO wiring.
// ---------------------------------------------------------------------------

interface ClassifiedFile {
    prs: { number: number; prType?: string | null }[];
}

interface PrFile {
    filename: string;
    patch?: string;
    status: string;
}

interface GhBlameRange {
    startingLine: number;
    endingLine: number;
    commit: {
        oid: string;
        author?: { user?: { login?: string } | null } | null;
    };
}

function isGenerated(path: string, excluded: string[]): boolean {
    return isExcludedPath(path, excluded);
}

function blameAtParent(
    owner: string,
    repo: string,
    parentRef: string,
    path: string,
    ranges: LineRange[],
): { commits: string[]; authors: string[] } {
    // GraphQL blame at the parent ref so removed lines resolve.
    const query = `query($owner:String!,$repo:String!,$ref:String!,$path:String!){repository(owner:$owner,name:$repo){object(expression:$ref){... on Commit{blame(path:$path){ranges{startingLine endingLine commit{oid author{user{login}}}}}}}}}`;
    const args = [
        "api",
        "graphql",
        "-f",
        `query=${query}`,
        "-F",
        `owner=${owner}`,
        "-F",
        `repo=${repo}`,
        "-F",
        `ref=${parentRef}`,
        "-F",
        `path=${path}`,
    ];
    const result = ghJsonSync<{
        data?: {
            repository?: {
                object?: { blame?: { ranges?: GhBlameRange[] } } | null;
            } | null;
        };
    }>(args);
    const blameRanges = result.data?.repository?.object?.blame?.ranges ?? [];
    const commits = new Set<string>();
    const authors = new Set<string>();
    for (const br of blameRanges) {
        const overlaps = ranges.some((r) =>
            rangesIntersect({ start: br.startingLine, end: br.endingLine }, r),
        );
        if (!overlaps) continue;
        commits.add(br.commit.oid);
        const login = br.commit.author?.user?.login;
        if (login) authors.add(login);
    }
    return { commits: [...commits], authors: [...authors] };
}

function commitToPrs(owner: string, repo: string, sha: string): number[] {
    const pulls = ghApiJsonPaginatedSync<{ number: number }[]>(
        `repos/${owner}/${repo}/commits/${sha}/pulls`,
    );
    return pulls.map((p) => p.number);
}

function ccrActivityForPr(
    owner: string,
    repo: string,
    prNumber: number,
    ccrLogins: string[],
): CcrActivity {
    const lower = new Set(ccrLogins.map((l) => l.toLowerCase()));
    const isCcr = (login: string | undefined): boolean =>
        login != null && lower.has(login.toLowerCase());

    const comments = ghApiJsonPaginatedSync<
        {
            path?: string;
            line?: number | null;
            start_line?: number | null;
            user?: { login?: string };
        }[]
    >(`repos/${owner}/${repo}/pulls/${prNumber}/comments`);
    const reviews = ghApiJsonPaginatedSync<{ user?: { login?: string } }[]>(
        `repos/${owner}/${repo}/pulls/${prNumber}/reviews`,
    );

    const inline: CcrActivity["inline"] = [];
    for (const c of comments) {
        if (!isCcr(c.user?.login) || !c.path) continue;
        const end = c.line ?? 0;
        const start = c.start_line ?? end;
        if (end > 0) inline.push({ path: c.path, range: { start, end } });
    }
    const hasSummary = reviews.some((r) => isCcr(r.user?.login));
    return { inline, hasSummary };
}

function traceFixPr(
    owner: string,
    repo: string,
    data: PullRequestData,
    excluded: string[],
    ccrLogins: string[],
    ccrEnabledSince: string | null,
): VerifiedMiss[] {
    const fixPr = data.pr.number;
    const fixUrl = data.pr.url;
    const headSha = data.commits.at(-1)?.sha;
    if (!headSha) return [];
    const parentRef = `${headSha}^`;

    const files = ghApiJsonPaginatedSync<PrFile[]>(
        `repos/${owner}/${repo}/pulls/${fixPr}/files`,
    );

    const misses: VerifiedMiss[] = [];
    for (const file of files) {
        const path = file.filename;
        if (isGenerated(path, excluded)) {
            misses.push(
                emptyMiss(fixPr, fixUrl, path, "unsupported-generated-file"),
            );
            continue;
        }
        if (file.status === "renamed") continue; // pure rename: not traceable
        const ranges = removedPreFixRanges(file.patch);
        if (ranges.length === 0) continue; // pure addition: nothing to blame

        const { commits, authors } = blameAtParent(
            owner,
            repo,
            parentRef,
            path,
            ranges,
        );
        if (commits.length === 0) {
            misses.push(emptyMiss(fixPr, fixUrl, path, "unresolved-no-pr"));
            continue;
        }

        const prNumbers: number[] = [];
        for (const sha of commits) {
            prNumbers.push(...commitToPrs(owner, repo, sha));
        }
        const { introducedByPr, traceOutcome } =
            resolveIntroducingPr(prNumbers);
        const blameConfidence = blameConfidenceOf(
            commits.length,
            authors.length,
        );

        if (traceOutcome !== "resolved" || introducedByPr == null) {
            misses.push({
                ...emptyMiss(fixPr, fixUrl, path, traceOutcome),
                introducingCommit: commits[0] ?? null,
                blameConfidence,
            });
            continue;
        }

        // Blame that spans multiple commits AND authors is too ambiguous to
        // call a verified miss; record it as low-confidence and never count it.
        if (blameConfidence === "low") {
            misses.push({
                fixPr,
                fixUrl,
                path,
                introducedByPr,
                introducedUrl: undefined,
                introducingCommit: commits[0] ?? null,
                traceOutcome: "low-confidence-refactor",
                ccrOpportunity: "ccrInactive",
                ccrActiveOnIntroducingPr: false,
                ccrCommentedOnLines: false,
                verifiedMiss: false,
                theme: null,
                blameConfidence,
            });
            continue;
        }

        const introData = ghApiJsonSync<{
            created_at?: string;
            html_url?: string;
        }>(`repos/${owner}/${repo}/pulls/${introducedByPr}`);
        const preEnablement =
            ccrEnabledSince != null &&
            introData.created_at != null &&
            Date.parse(introData.created_at) < Date.parse(ccrEnabledSince);

        const ccr: CcrActivity = preEnablement
            ? { inline: [], hasSummary: false }
            : ccrActivityForPr(owner, repo, introducedByPr, ccrLogins);
        const verdict = gradeOpportunity(path, ranges, ccr);

        misses.push({
            fixPr,
            fixUrl,
            path,
            introducedByPr,
            introducedUrl: introData.html_url,
            introducingCommit: commits[0] ?? null,
            traceOutcome: "resolved",
            ccrOpportunity: verdict.ccrOpportunity,
            ccrActiveOnIntroducingPr: verdict.ccrActiveOnIntroducingPr,
            ccrCommentedOnLines: verdict.ccrCommentedOnLines,
            verifiedMiss: verdict.verifiedMiss,
            theme: null,
            blameConfidence,
        });
    }
    return misses;
}

function emptyMiss(
    fixPr: number,
    fixUrl: string | undefined,
    path: string,
    traceOutcome: TraceOutcome,
): VerifiedMiss {
    return {
        fixPr,
        fixUrl,
        path,
        introducedByPr: null,
        introducedUrl: undefined,
        introducingCommit: null,
        traceOutcome,
        ccrOpportunity: "ccrInactive",
        ccrActiveOnIntroducingPr: false,
        ccrCommentedOnLines: false,
        verifiedMiss: false,
        theme: null,
        blameConfidence: "low",
    };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/trace-bug-origin.ts --repo <owner/repo> --classified <cache>/classified.json \\",
        "    --glob 'pr-cache/<o>-<r>/pr-*.json' --cache-dir <cache> [options]",
        "",
        "Options:",
        "  --repo <owner/repo>      Target repository",
        "  --classified <path>      classified.json (to pick bug-fix PRs)",
        "  --glob <pattern>         Raw PR cache files",
        "  --config <path>          Override config.json",
        "  --cache-dir <path>       Write traced.json here (else stdout)",
        "  --json                   Emit machine-readable result to stdout",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            repo: { type: "string" },
            classified: { type: "string" },
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
        return;
    }
    const v = parsed.values;
    if (!v.repo) throw new Error("--repo is required");
    if (!v.classified) throw new Error("--classified is required");
    const [owner, repo] = v.repo.split("/");
    if (!owner || !repo) throw new Error('--repo must be "owner/repo"');

    const cfg = loadConfig(v.config);
    const classified = JSON.parse(
        fs.readFileSync(v.classified, "utf8"),
    ) as ClassifiedFile;
    const bugFixNumbers = new Set(
        classified.prs
            .filter((p) => p.prType === "bug-fix")
            .map((p) => p.number),
    );

    const files = [...parsed.positionals];
    if (v.glob) files.push(...fs.globSync(v.glob, { withFileTypes: false }));

    const bugFixData: PullRequestData[] = [];
    for (const file of files) {
        const data = JSON.parse(
            fs.readFileSync(file, "utf8"),
        ) as PullRequestData;
        if (bugFixNumbers.has(data.pr.number) && !isBot(data.pr.author)) {
            bugFixData.push(data);
        }
    }

    const verifiedMisses: VerifiedMiss[] = [];
    for (const data of bugFixData) {
        verifiedMisses.push(
            ...traceFixPr(
                owner,
                repo,
                data,
                cfg.excludedPaths,
                cfg.ccrLogins,
                cfg.ccrEnabledSince,
            ),
        );
    }

    const payload = {
        processed: bugFixData.length,
        kept: verifiedMisses.length,
        skipped: 0,
        skipReasons: {
            verified: verifiedMisses.filter((m) => m.verifiedMiss).length,
        },
        verifiedMisses,
    };

    if (v["cache-dir"]) {
        fs.mkdirSync(v["cache-dir"], { recursive: true });
        const out = `${v["cache-dir"]}/traced.json`;
        fs.writeFileSync(out, JSON.stringify(payload, null, 2));
        log.error(
            `wrote ${out} (${String(verifiedMisses.length)} traces, ${String(payload.skipReasons.verified)} verified misses)`,
        );
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
